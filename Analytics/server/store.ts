import { DatabaseSync } from "node:sqlite";
import { randomUUID } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { ApiError, check, digest, noticeVersion, submission } from "./model.ts";
import type { Submission } from "./model.ts";
import { makeBinding } from "./bindings.ts";
import type { DevicePack } from "./bindings.ts";

export type Participant = {
  id: string;
  code: string;
  nickname: string | null;
  registeredAt: string;
  consentedAt: string | null;
};
export type Session = Omit<Submission, "missions" | "distractions" | "quiz"> & {
  missions: (Submission["missions"][number] & {
    timeSeconds: number | null;
    focusSeconds: number | null;
    distractionCount: number | null;
  })[];
  distractions: (Submission["distractions"][number] & {
    missionId: string | null;
  })[];
  quiz:
    | (NonNullable<Submission["quiz"]> & {
        incorrect: number;
        percentage: number | null;
      })
    | null;
};

export type Records = { participants: Participant[]; sessions: Session[] };
type AsyncCapable<T> = T extends (...args: infer A) => infer R
  ? (...args: A) => R | Promise<R>
  : never;
type StoreMethod =
  | "register"
  | "consent"
  | "remove"
  | "bind"
  | "bindMany"
  | "openPairing"
  | "pairedPack"
  | "approvePairing"
  | "enrollPlayer"
  | "resetPlayerCodes"
  | "record"
  | "records"
  | "prune";
export type AnalyticsStore = { [K in StoreMethod]: AsyncCapable<Store[K]> };

export class Store {
  db: DatabaseSync;
  constructor(path: string) {
    if (path !== ":memory:")
      mkdirSync(dirname(resolve(path)), { recursive: true, mode: 0o700 });
    this.db = new DatabaseSync(path);
    this.db.exec(
      "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL; PRAGMA secure_delete=ON; CREATE TABLE IF NOT EXISTS migrations (version TEXT PRIMARY KEY NOT NULL) STRICT",
    );
    const folder = new URL("../migrations/", import.meta.url);
    for (const version of readdirSync(folder)
      .filter((f) => /^\d+_.*\.sql$/.test(f))
      .sort()) {
      this.transaction(() => {
        if (
          this.db
            .prepare("SELECT 1 FROM migrations WHERE version=?")
            .get(version)
        )
          return;
        this.db.exec(readFileSync(new URL(version, folder), "utf8"));
        this.db.prepare("INSERT INTO migrations VALUES (?)").run(version);
      });
    }
  }
  transaction<T>(fn: () => T): T {
    this.db.exec("BEGIN IMMEDIATE");
    try {
      const result = fn();
      this.db.exec("COMMIT");
      return result;
    } catch (err) {
      this.db.exec("ROLLBACK");
      throw err;
    }
  }
  register(nickname: string | null) {
    return this.transaction(() => this.insertParticipant(nickname));
  }
  private insertParticipant(nickname: string | null) {
    const id = randomUUID(),
      at = new Date().toISOString();
    const { value } = this.db
      .prepare("UPDATE code_sequence SET value=value+1 RETURNING value")
      .get()!;
    const code = "P" + String(value).padStart(3, "0");
    this.db
      .prepare("INSERT INTO participants VALUES (?,?,?,?,?)")
      .run(id, value, code, nickname, at);
    this.db
      .prepare(
        "INSERT INTO consent_records(participant_id,notice_version,granted_at) VALUES (?,?,?)",
      )
      .run(id, noticeVersion, at);
    return { id, code, nickname, registeredAt: at, consentedAt: at };
  }
  participantExists(id: string) {
    if (!this.db.prepare("SELECT 1 FROM participants WHERE id=?").get(id))
      throw new ApiError(404, "Participant not found");
  }
  consent(id: string, granted: boolean) {
    this.transaction(() => {
      this.participantExists(id);
      const at = new Date().toISOString();
      if (granted) {
        this.db
          .prepare(
            "INSERT OR IGNORE INTO consent_records(participant_id,notice_version,granted_at) VALUES (?,?,?)",
          )
          .run(id, noticeVersion, at);
      } else {
        this.db
          .prepare(
            "UPDATE consent_records SET withdrawn_at=? WHERE participant_id=? AND withdrawn_at IS NULL",
          )
          .run(at, id);
        this.db
          .prepare("DELETE FROM upload_grants WHERE participant_id=?")
          .run(id);
        this.db
          .prepare("DELETE FROM game_pairings WHERE participant_id=?")
          .run(id);
        this.db
          .prepare(
            "UPDATE game_starts SET sealed_pack=NULL WHERE participant_id=?",
          )
          .run(id);
      }
    });
  }
  enrollPlayer(
    requestHash: string,
    endpoint: string,
    days: number,
    now: string,
    expiresAt: string,
    seal: (pack: DevicePack) => string,
  ) {
    return this.transaction(() => {
      const previous = this.db
        .prepare(
          "SELECT sealed_pack,expires_at FROM game_starts WHERE request_hash=?",
        )
        .get(requestHash);
      if (previous) {
        if (String(previous.expires_at) <= now)
          throw new ApiError(409, "Start request expired");
        if (previous.sealed_pack === null)
          throw new ApiError(403, "Consent withdrawn");
        return String(previous.sealed_pack);
      }
      const participant = this.insertParticipant(null);
      const sealed = seal(
        this.issueBindings(participant.id, endpoint, days, 1),
      );
      this.db
        .prepare("INSERT INTO game_starts VALUES (?,?,?,?)")
        .run(requestHash, participant.id, sealed, expiresAt);
      return sealed;
    });
  }
  remove(id: string) {
    if (!this.db.prepare("DELETE FROM participants WHERE id=?").run(id).changes)
      throw new ApiError(404, "Participant not found");
  }
  resetPlayerCodes() {
    this.transaction(() => {
      if (this.db.prepare("SELECT 1 FROM participants LIMIT 1").get())
        throw new ApiError(409, "The participant directory must be empty");
      this.db.prepare("UPDATE code_sequence SET value=0").run();
    });
  }
  bind(participantId: string, endpoint: string, days: number) {
    return this.bindMany(participantId, endpoint, days, 1).bindings[0];
  }
  bindMany(
    participantId: string,
    endpoint: string,
    days: number,
    runs: number,
  ) {
    return this.transaction(() =>
      this.issueBindings(participantId, endpoint, days, runs),
    );
  }
  private issueBindings(
    participantId: string,
    endpoint: string,
    days: number,
    runs: number,
  ): DevicePack {
    const participant = this.db
      .prepare("SELECT code FROM participants WHERE id=?")
      .get(participantId);
    if (!participant) throw new ApiError(404, "Participant not found");
    const active = this.db
      .prepare(
        "SELECT id FROM consent_records WHERE participant_id=? AND withdrawn_at IS NULL",
      )
      .get(participantId);
    if (!active) throw new ApiError(409, "Active consent required");
    const now = new Date();
    const insert = this.db.prepare(
      "INSERT INTO upload_grants VALUES (?,?,?,?,?,?)",
    );
    const bindings = Array.from({ length: runs }, () => {
      const binding = makeBinding(participantId, endpoint, days, now);
      insert.run(
        binding.sessionId,
        participantId,
        active.id,
        digest(binding.token),
        now.toISOString(),
        binding.expiresAt,
      );
      return binding;
    });
    return { participantCode: String(participant.code), bindings };
  }
  openPairing(
    code: string,
    secretHash: string,
    now: string,
    expiresAt: string,
  ) {
    return this.transaction(() => {
      this.db.prepare("DELETE FROM game_pairings WHERE expires_at<=?").run(now);
      return Boolean(
        this.db
          .prepare(
            "INSERT OR IGNORE INTO game_pairings(code,secret_hash,expires_at) VALUES (?,?,?)",
          )
          .run(code, secretHash, expiresAt).changes,
      );
    });
  }
  pairedPack(code: string, secretHash: string, now: string) {
    const row = this.db
      .prepare(
        "SELECT sealed_pack FROM game_pairings WHERE code=? AND secret_hash=? AND expires_at>?",
      )
      .get(code, secretHash, now);
    if (!row) throw new ApiError(404, "Pairing unavailable or expired");
    return row.sealed_pack === null ? null : String(row.sealed_pack);
  }
  approvePairing(
    code: string,
    participantId: string,
    endpoint: string,
    days: number,
    runs: number,
    seal: (pack: DevicePack) => string,
  ) {
    this.transaction(() => {
      const pair = this.db
        .prepare(
          "SELECT sealed_pack FROM game_pairings WHERE code=? AND expires_at>?",
        )
        .get(code, new Date().toISOString());
      if (!pair) throw new ApiError(404, "Pairing unavailable or expired");
      if (pair.sealed_pack !== null)
        throw new ApiError(409, "Pairing already approved");
      const pack = this.issueBindings(participantId, endpoint, days, runs);
      this.db
        .prepare(
          "UPDATE game_pairings SET participant_id=?, sealed_pack=? WHERE code=?",
        )
        .run(participantId, seal(pack), code);
    });
  }
  record(token: string, raw: unknown, retentionDays: number) {
    return this.transaction(() => {
      const grant = this.db
        .prepare(
          "SELECT g.* FROM upload_grants g JOIN consent_records c ON c.id=g.consent_id WHERE token_hash=? AND expires_at>? AND c.withdrawn_at IS NULL",
        )
        .get(digest(token), new Date().toISOString());
      if (!grant)
        throw new ApiError(403, "Invalid or expired session credential");
      const s = submission(raw);
      if (s.id !== grant.session_id || s.participantId !== grant.participant_id)
        throw new ApiError(403, "Credential does not match session");
      check(
        Date.parse(s.startedAt) >= Date.parse(String(grant.issued_at)) - 300000,
        "Session predates device binding",
      );
      check(
        Date.parse(s.endedAt) >= Date.now() - retentionDays * 86400000,
        "Session outside retention period",
      );
      const hash = digest(JSON.stringify(s));
      const previous = this.db
        .prepare("SELECT payload_hash FROM sessions WHERE id=?")
        .get(s.id);
      if (previous) {
        if (previous.payload_hash !== hash)
          throw new ApiError(
            409,
            "Session already recorded with different values",
          );
        return false;
      }
      this.insert(s, hash, Number(grant.consent_id), String(grant.issued_at));
      return true;
    });
  }
  private insert(
    s: Submission,
    hash: string,
    consentId: number,
    confirmedAt: string,
  ) {
    this.db
      .prepare(
        `INSERT INTO sessions
      (id,participant_id,started_at,ended_at,status,duration_seconds,game_version,payload_hash,consent_record_id,consent_confirmed_at)
      VALUES (?,?,?,?,?,?,?,?,?,?)`,
      )
      .run(
        s.id,
        s.participantId,
        s.startedAt,
        s.endedAt,
        s.status,
        s.durationSeconds,
        s.gameVersion,
        hash,
        consentId,
        confirmedAt,
      );
    const mission = this.db.prepare(
      "INSERT INTO mission_results(session_id,mission_id,completed,items_done,items_total) VALUES (?,?,?,?,?)",
    );
    for (const m of s.missions)
      mission.run(s.id, m.id, Number(m.completed), m.itemsDone, m.itemsTotal);
    const distraction = this.db.prepare(
      "INSERT INTO distraction_results(session_id,kind,count,seconds) VALUES (?,?,?,?)",
    );
    for (const d of s.distractions)
      distraction.run(s.id, d.kind, d.count, d.seconds);
    if (s.quiz)
      this.db
        .prepare(
          "INSERT INTO quiz_results(session_id,total,answered,correct) VALUES (?,?,?,?)",
        )
        .run(s.id, s.quiz.total, s.quiz.answered, s.quiz.correct);
  }
  records(): { participants: Participant[]; sessions: Session[] } {
    const db = this.db;
    db.exec("BEGIN");
    try {
      const participants = db
        .prepare(
          "SELECT p.id,p.code,p.nickname,p.registered_at registeredAt,c.granted_at consentedAt FROM participants p LEFT JOIN consent_records c ON c.participant_id=p.id AND c.withdrawn_at IS NULL ORDER BY p.sequence",
        )
        .all() as unknown as Participant[];
      const rows = db
        .prepare(
          "SELECT id,participant_id participantId,started_at startedAt,ended_at endedAt,status,duration_seconds durationSeconds,game_version gameVersion FROM sessions ORDER BY started_at DESC,id",
        )
        .all();
      const sessions = rows.map((row) => ({
        ...row,
        missions: [],
        distractions: [],
        quiz: null,
      })) as unknown as Session[];
      const map = new Map(sessions.map((s) => [s.id, s]));
      for (const { sessionId, ...m } of db
        .prepare(
          "SELECT session_id sessionId,mission_id id,completed,items_done itemsDone,items_total itemsTotal,time_seconds timeSeconds,focus_seconds focusSeconds,distraction_count distractionCount FROM mission_results ORDER BY rowid",
        )
        .all()) {
        map.get(String(sessionId))!.missions.push({
          ...m,
          completed: Boolean(m.completed),
        } as Session["missions"][number]);
      }
      for (const { sessionId, ...d } of db
        .prepare(
          "SELECT session_id sessionId,kind,count,seconds,mission_id missionId FROM distraction_results ORDER BY rowid",
        )
        .all())
        map
          .get(String(sessionId))!
          .distractions.push(d as Session["distractions"][number]);
      for (const { sessionId, ...q } of db
        .prepare(
          "SELECT session_id sessionId,total,answered,correct,incorrect,percentage FROM quiz_results",
        )
        .all())
        map.get(String(sessionId))!.quiz = q as Session["quiz"];
      db.exec("COMMIT");
      return { participants, sessions };
    } catch (err) {
      db.exec("ROLLBACK");
      throw err;
    }
  }
  prune(days: number, now = Date.now()) {
    const cutoff = new Date(now - days * 86400000).toISOString();
    this.transaction(() => {
      this.db
        .prepare("DELETE FROM game_pairings WHERE expires_at<=?")
        .run(new Date(now).toISOString());
      this.db
        .prepare("DELETE FROM game_starts WHERE expires_at<=?")
        .run(new Date(now).toISOString());
      this.db
        .prepare("DELETE FROM upload_grants WHERE expires_at<=?")
        .run(new Date(now).toISOString());
      this.db.prepare("DELETE FROM sessions WHERE ended_at<?").run(cutoff);
      this.db
        .prepare(
          `DELETE FROM participants WHERE registered_at<? AND NOT EXISTS
        (SELECT 1 FROM sessions WHERE participant_id=participants.id) AND NOT EXISTS
        (SELECT 1 FROM upload_grants WHERE participant_id=participants.id) AND NOT EXISTS
        (SELECT 1 FROM consent_records WHERE participant_id=participants.id AND granted_at>=?)`,
        )
        .run(cutoff, cutoff);
    });
  }
}
