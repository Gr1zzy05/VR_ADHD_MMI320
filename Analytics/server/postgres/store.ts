import { randomUUID } from "node:crypto";
import { makeBinding } from "../bindings.ts";
import type { DevicePack } from "../bindings.ts";
import { readFile } from "node:fs/promises";
import {
  ApiError,
  check,
  digest,
  noticeVersion,
  submission,
} from "../model.ts";
import type { AnalyticsStore, Records } from "../store.ts";

export interface Connection {
  query<T extends object = Record<string, unknown>>(
    sql: string,
    values?: unknown[],
  ): Promise<{ rows: T[] }>;
}
type TransactionConnection = Connection & {
  executeScript(sql: string): Promise<unknown>;
};
export type Transaction = <T>(
  work: (db: TransactionConnection) => Promise<T>,
) => Promise<T>;
type Role = "admin" | "educator";
export interface StaffSessions {
  find(key: string, now: number): Promise<Role | undefined>;
  save(key: string, role: Role, expires: number): Promise<void>;
  removeStaff(key: string): Promise<void>;
  rate(
    key: string,
    now: number,
    period: number,
  ): Promise<{ count: number; until: number }>;
}
const iso = (ms = Date.now()) => new Date(ms).toISOString();
const maxClockSkew = 300000;
const day = 86400000;

export class PostgresStore implements AnalyticsStore, StaffSessions {
  private readonly db: Connection;
  private readonly transaction: Transaction;

  constructor(db: Connection, transaction: Transaction) {
    this.db = db;
    this.transaction = transaction;
  }

  async migrate() {
    const migrations = [
      await readFile(new URL("./001_initial.sql", import.meta.url), "utf8"),
      await readFile(
        new URL("./002_game_pairing.sql", import.meta.url),
        "utf8",
      ),
      await readFile(
        new URL("./003_automatic_players.sql", import.meta.url),
        "utf8",
      ),
    ];
    await this.write(async (db) => {
      await db.query(
        "CREATE TABLE IF NOT EXISTS analytics_migrations (version INTEGER PRIMARY KEY)",
      );
      for (const [index, sql] of migrations.entries()) {
        const version = index + 1;
        if (
          (
            await db.query(
              "SELECT 1 FROM analytics_migrations WHERE version=$1",
              [version],
            )
          ).rows.length
        )
          continue;
        await db.executeScript(sql);
        await db.query("INSERT INTO analytics_migrations VALUES ($1)", [
          version,
        ]);
      }
    });
  }

  private write<T>(work: (db: TransactionConnection) => Promise<T>) {
    return this.transaction(async (db) => {
      // Serialize study updates, including consent withdrawal and retry of a pending upload.
      await db.query("SELECT pg_advisory_xact_lock(1482052423)");
      return work(db);
    });
  }

  register(nickname: string | null) {
    return this.write((db) => this.insertParticipant(db, nickname));
  }
  private async insertParticipant(db: Connection, nickname: string | null) {
    const id = randomUUID(),
      at = iso();
    const { value } = (
      await db.query<{ value: number }>(
        "UPDATE code_sequence SET value=value+1 RETURNING value",
      )
    ).rows[0];
    const code = "P" + String(value).padStart(3, "0");
    await db.query("INSERT INTO participants VALUES ($1,$2,$3,$4,$5)", [
      id,
      value,
      code,
      nickname,
      at,
    ]);
    await db.query(
      "INSERT INTO consent_records(participant_id,notice_version,granted_at) VALUES ($1,$2,$3)",
      [id, noticeVersion, at],
    );
    return { id, code, nickname, registeredAt: at, consentedAt: at };
  }

  private async participantExists(db: Connection, id: string) {
    if (
      !(await db.query("SELECT 1 FROM participants WHERE id=$1", [id])).rows
        .length
    )
      throw new ApiError(404, "Participant not found");
  }

  consent(id: string, granted: boolean) {
    return this.write(async (db) => {
      await this.participantExists(db, id);
      if (granted) {
        await db.query(
          "INSERT INTO consent_records(participant_id,notice_version,granted_at) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING",
          [id, noticeVersion, iso()],
        );
      } else {
        await db.query(
          "UPDATE consent_records SET withdrawn_at=$1 WHERE participant_id=$2 AND withdrawn_at IS NULL",
          [iso(), id],
        );
        await db.query("DELETE FROM upload_grants WHERE participant_id=$1", [
          id,
        ]);
        await db.query("DELETE FROM game_pairings WHERE participant_id=$1", [
          id,
        ]);
        await db.query(
          "UPDATE game_starts SET sealed_pack=NULL WHERE participant_id=$1",
          [id],
        );
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
    return this.write(async (db) => {
      const {
        rows: [previous],
      } = await db.query<{ sealed_pack: string | null; expires_at: string }>(
        "SELECT sealed_pack,expires_at FROM game_starts WHERE request_hash=$1",
        [requestHash],
      );
      if (previous) {
        if (previous.expires_at <= now)
          throw new ApiError(409, "Start request expired");
        if (previous.sealed_pack === null)
          throw new ApiError(403, "Consent withdrawn");
        return previous.sealed_pack;
      }
      const participant = await this.insertParticipant(db, null);
      const sealed = seal(
        await this.issueBindings(db, participant.id, endpoint, days, 1),
      );
      await db.query("INSERT INTO game_starts VALUES ($1,$2,$3,$4)", [
        requestHash,
        participant.id,
        sealed,
        expiresAt,
      ]);
      return sealed;
    });
  }

  remove(id: string) {
    return this.write(async (db) => {
      if (
        !(
          await db.query("DELETE FROM participants WHERE id=$1 RETURNING id", [
            id,
          ])
        ).rows.length
      )
        throw new ApiError(404, "Participant not found");
    });
  }

  async bind(participantId: string, endpoint: string, days: number) {
    return (await this.bindMany(participantId, endpoint, days, 1)).bindings[0];
  }

  bindMany(
    participantId: string,
    endpoint: string,
    days: number,
    runs: number,
  ) {
    return this.write((db) =>
      this.issueBindings(db, participantId, endpoint, days, runs),
    );
  }

  private async issueBindings(
    db: Connection,
    participantId: string,
    endpoint: string,
    days: number,
    runs: number,
  ): Promise<DevicePack> {
    const {
      rows: [participant],
    } = await db.query<{ code: string }>(
      "SELECT code FROM participants WHERE id=$1",
      [participantId],
    );
    if (!participant) throw new ApiError(404, "Participant not found");
    const active = (
      await db.query<{ id: number }>(
        "SELECT id FROM consent_records WHERE participant_id=$1 AND withdrawn_at IS NULL",
        [participantId],
      )
    ).rows[0];
    if (!active) throw new ApiError(409, "Active consent required");
    const issuedAt = new Date();
    const bindings = Array.from({ length: runs }, () =>
      makeBinding(participantId, endpoint, days, issuedAt),
    );
    const values = bindings.flatMap((binding) => [
      binding.sessionId,
      participantId,
      active.id,
      digest(binding.token),
      issuedAt.toISOString(),
      binding.expiresAt,
    ]);
    const rows = bindings.map((_, index) => {
      const offset = index * 6;
      return `($${offset + 1},$${offset + 2},$${offset + 3},$${offset + 4},$${offset + 5},$${offset + 6})`;
    });
    await db.query(
      "INSERT INTO upload_grants VALUES " + rows.join(","),
      values,
    );
    return { participantCode: participant.code, bindings };
  }
  openPairing(
    code: string,
    secretHash: string,
    now: string,
    expiresAt: string,
  ) {
    return this.write(async (db) => {
      await db.query("DELETE FROM game_pairings WHERE expires_at<=$1", [now]);
      const { rows } = await db.query(
        "INSERT INTO game_pairings(code,secret_hash,expires_at) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING RETURNING code",
        [code, secretHash, expiresAt],
      );
      return rows.length > 0;
    });
  }
  async pairedPack(code: string, secretHash: string, now: string) {
    const {
      rows: [pair],
    } = await this.db.query<{ sealed_pack: string | null }>(
      "SELECT sealed_pack FROM game_pairings WHERE code=$1 AND secret_hash=$2 AND expires_at>$3",
      [code, secretHash, now],
    );
    if (!pair) throw new ApiError(404, "Pairing unavailable or expired");
    return pair.sealed_pack;
  }
  approvePairing(
    code: string,
    participantId: string,
    endpoint: string,
    days: number,
    runs: number,
    seal: (pack: DevicePack) => string,
  ) {
    return this.write(async (db) => {
      const {
        rows: [pair],
      } = await db.query<{ sealed_pack: string | null }>(
        "SELECT sealed_pack FROM game_pairings WHERE code=$1 AND expires_at>$2",
        [code, iso()],
      );
      if (!pair) throw new ApiError(404, "Pairing unavailable or expired");
      if (pair.sealed_pack !== null)
        throw new ApiError(409, "Pairing already approved");
      const pack = await this.issueBindings(
        db,
        participantId,
        endpoint,
        days,
        runs,
      );
      await db.query(
        "UPDATE game_pairings SET participant_id=$1,sealed_pack=$2 WHERE code=$3",
        [participantId, seal(pack), code],
      );
    });
  }

  record(token: string, raw: unknown, retentionDays: number) {
    return this.write(async (db) => {
      const grant = (
        await db.query<{
          session_id: string;
          participant_id: string;
          issued_at: string;
          consent_id: number;
        }>(
          "SELECT g.* FROM upload_grants g JOIN consent_records c ON c.id=g.consent_id WHERE token_hash=$1 AND expires_at>$2 AND c.withdrawn_at IS NULL",
          [digest(token), iso()],
        )
      ).rows[0];
      if (!grant)
        throw new ApiError(403, "Invalid or expired session credential");
      const s = submission(raw);
      if (s.id !== grant.session_id || s.participantId !== grant.participant_id)
        throw new ApiError(403, "Credential does not match session");
      check(
        Date.parse(s.startedAt) >= Date.parse(grant.issued_at) - maxClockSkew,
        "Session predates device binding",
      );
      check(
        Date.parse(s.endedAt) >= Date.now() - retentionDays * day,
        "Session outside retention period",
      );
      const hash = digest(JSON.stringify(s));
      const inserted = await db.query(
        `INSERT INTO sessions
        (id,participant_id,started_at,ended_at,status,duration_seconds,game_version,payload_hash,consent_record_id,consent_confirmed_at)
        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) ON CONFLICT (id) DO NOTHING RETURNING id`,
        [
          s.id,
          s.participantId,
          s.startedAt,
          s.endedAt,
          s.status,
          s.durationSeconds,
          s.gameVersion,
          hash,
          grant.consent_id,
          grant.issued_at,
        ],
      );
      if (!inserted.rows.length) {
        const previous = (
          await db.query<{ payload_hash: string }>(
            "SELECT payload_hash FROM sessions WHERE id=$1",
            [s.id],
          )
        ).rows[0];
        if (previous?.payload_hash !== hash)
          throw new ApiError(
            409,
            "Session already recorded with different values",
          );
        return false;
      }
      for (const m of s.missions)
        await db.query(
          "INSERT INTO mission_results(session_id,mission_id,completed,items_done,items_total) VALUES ($1,$2,$3,$4,$5)",
          [s.id, m.id, Number(m.completed), m.itemsDone, m.itemsTotal],
        );
      for (const d of s.distractions)
        await db.query(
          "INSERT INTO distraction_results(session_id,kind,count,seconds) VALUES ($1,$2,$3,$4)",
          [s.id, d.kind, d.count, d.seconds],
        );
      if (s.quiz)
        await db.query(
          "INSERT INTO quiz_results(session_id,total,answered,correct) VALUES ($1,$2,$3,$4)",
          [s.id, s.quiz.total, s.quiz.answered, s.quiz.correct],
        );
      return true;
    });
  }

  async records(): Promise<Records> {
    const { rows } = await this.db.query<Records>(`
      SELECT
        COALESCE((SELECT json_agg(json_build_object('id',p.id,'code',p.code,'nickname',p.nickname,
          'registeredAt',p.registered_at,'consentedAt',c.granted_at) ORDER BY p.sequence)
          FROM participants p LEFT JOIN consent_records c ON c.participant_id=p.id AND c.withdrawn_at IS NULL), '[]'::json) AS participants,
        COALESCE((SELECT json_agg(json_build_object(
          'id',s.id,'participantId',s.participant_id,'startedAt',s.started_at,'endedAt',s.ended_at,
          'status',s.status,'durationSeconds',s.duration_seconds,'gameVersion',s.game_version,
          'missions',COALESCE((SELECT json_agg(json_build_object('id',m.mission_id,'completed',m.completed=1,
            'itemsDone',m.items_done,'itemsTotal',m.items_total,'timeSeconds',m.time_seconds,
            'focusSeconds',m.focus_seconds,'distractionCount',m.distraction_count) ORDER BY
            array_position(ARRAY['washing','organizing','trash','meeting'],m.mission_id))
            FROM mission_results m WHERE m.session_id=s.id), '[]'::json),
          'distractions',COALESCE((SELECT json_agg(json_build_object('kind',d.kind,'count',d.count,
            'seconds',d.seconds,'missionId',d.mission_id) ORDER BY d.kind) FROM distraction_results d WHERE d.session_id=s.id),'[]'::json),
          'quiz',(SELECT json_build_object('total',q.total,'answered',q.answered,'correct',q.correct,
            'incorrect',q.incorrect,'percentage',q.percentage) FROM quiz_results q WHERE q.session_id=s.id)
        ) ORDER BY s.started_at DESC,s.id) FROM sessions s), '[]'::json) AS sessions
    `);
    return rows[0];
  }

  prune(days: number, now = Date.now()) {
    return this.write(async (db) => {
      await db.query("DELETE FROM game_pairings WHERE expires_at<=$1", [
        iso(now),
      ]);
      await db.query("DELETE FROM game_starts WHERE expires_at<=$1", [
        iso(now),
      ]);
      const cutoff = iso(now - days * day);
      await db.query("DELETE FROM upload_grants WHERE expires_at<=$1", [
        iso(now),
      ]);
      await db.query("DELETE FROM sessions WHERE ended_at<$1", [cutoff]);
      await db.query(
        `DELETE FROM participants WHERE registered_at<$1 AND NOT EXISTS
        (SELECT 1 FROM sessions WHERE participant_id=participants.id) AND NOT EXISTS
        (SELECT 1 FROM upload_grants WHERE participant_id=participants.id) AND NOT EXISTS
        (SELECT 1 FROM consent_records WHERE participant_id=participants.id AND granted_at>=$1)`,
        [cutoff],
      );
      await db.query("DELETE FROM staff_logins WHERE expires_at<=$1", [now]);
      await db.query("DELETE FROM request_limits WHERE until_at<=$1", [now]);
    });
  }

  async find(key: string, now: number) {
    return (
      await this.db.query<{ role: Role }>(
        "SELECT role FROM staff_logins WHERE token_hash=$1 AND expires_at>$2",
        [key, now],
      )
    ).rows[0]?.role;
  }
  async save(key: string, role: Role, expires: number) {
    await this.db.query("INSERT INTO staff_logins VALUES ($1,$2,$3)", [
      key,
      role,
      expires,
    ]);
  }
  async removeStaff(key: string) {
    await this.db.query("DELETE FROM staff_logins WHERE token_hash=$1", [key]);
  }
  async rate(key: string, now: number, period: number) {
    const { rows } = await this.db.query<{ count: number; until: number }>(
      `
      INSERT INTO request_limits VALUES ($1,1,$2::bigint)
      ON CONFLICT (key) DO UPDATE SET
        count=CASE WHEN request_limits.until_at <= $3::bigint THEN 1 ELSE request_limits.count+1 END,
        until_at=CASE WHEN request_limits.until_at <= $3::bigint THEN EXCLUDED.until_at ELSE request_limits.until_at END
      RETURNING count, until_at::double precision AS until`,
      [key, now + period, now],
    );
    return rows[0];
  }
}
