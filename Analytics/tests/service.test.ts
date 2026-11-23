import test from "node:test";
import type { TestContext } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { once } from "node:events";
import type { AddressInfo } from "node:net";
import { application } from "../server/http.ts";
import { Store } from "../server/store.ts";
import { missionIds, kinds } from "../server/model.ts";

const consent = { consent: true, noticeVersion: "2026-01" };
async function fixture(t: TestContext) {
  const dir = mkdtempSync(join(tmpdir(), "focus-shift-test-"));
  const file = join(dir, "test.sqlite"),
    db = new Store(file);
  const { app, close } = application(db, {
    demo: false,
    origin: "https://study.example",
    retentionDays: 60,
    grantDays: 7,
    adminUser: "staff",
    adminPassword: "random-admin-test-password",
    educatorUser: "reader",
    educatorPassword: "random-reader-test-password",
  });
  const server = app.listen(0, "127.0.0.1");
  await once(server, "listening");
  t.after(done);
  const base =
    "http://127.0.0.1:" + (server.address() as AddressInfo).port + "/api/";
  const req = (
    path: string,
    method = "GET",
    body?: unknown,
    cookie = "",
    extra: Record<string, string> = {},
  ) =>
    fetch(base + path, {
      method,
      headers: { "Content-Type": "application/json", Cookie: cookie, ...extra },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  async function login(username: string, password: string) {
    const res = await req("login", "POST", { username, password });
    assert.equal(res.status, 200);
    const cookie = res.headers.get("set-cookie")!;
    assert.match(cookie, /HttpOnly/);
    assert.match(cookie, /SameSite=Strict/);
    assert.match(cookie, /Secure/);
    return cookie.split(";")[0];
  }
  const admin = await login("staff", "random-admin-test-password"),
    reader = await login("reader", "random-reader-test-password");
  const add = async (nickname: string | null = null) => {
    const r = await req(
      "participants",
      "POST",
      { nickname, ...consent },
      admin,
    );
    assert.equal(r.status, 201);
    return await r.json();
  };
  const bind = async (id: string) => {
    const r = await req(
      "participants/" + id + "/binding",
      "POST",
      consent,
      admin,
    );
    assert.equal(r.status, 200);
    return await r.json();
  };
  function snapshot(
    g: { participantId: string; sessionId: string },
    complete = true,
  ) {
    const time = Date.now();
    return {
      id: g.sessionId,
      participantId: g.participantId,
      startedAt: new Date(time - 90000).toISOString(),
      endedAt: new Date(time).toISOString(),
      durationSeconds: complete ? 83 : 0,
      status: complete ? "completed" : "interrupted",
      gameVersion: "1.0",
      missions: missionIds.map((id, i) =>
        id === "meeting"
          ? { id, completed: complete }
          : {
              id,
              completed: complete || i === 0,
              itemsDone: complete || i === 0 ? 3 : 0,
              itemsTotal: 3,
            },
      ),
      distractions: kinds.map((kind, i) => ({
        kind,
        count: i === 5 ? 2 : 0,
        seconds: i === 5 ? 5.2 : 0,
      })),
      quiz: complete ? { total: 3, answered: 3, correct: 0 } : null,
    };
  }
  const upload = (token: string, b: unknown) =>
    req("submissions", "POST", b, "", { Authorization: "Bearer " + token });
  async function done() {
    await new Promise<void>((resolve, reject) =>
      server.close((e) => (e ? reject(e) : resolve())),
    );
    close();
    db.db.close();
    rmSync(dir, { force: true, recursive: true });
  }
  return { db, file, req, admin, reader, add, bind, snapshot, upload };
}

test("Enrollment, two distinct sessions, persistent mission/quiz/ledger rows, measured zero and unavailable values", async (t) => {
  const f = await fixture(t);
  const p = await f.add("Synthetic Fern"),
    empty = await f.add();
  assert.equal(p.code, "P001");
  assert.equal(empty.code, "P002");
  assert.notEqual(p.id, empty.id);
  const g = await f.bind(p.id),
    h = await f.bind(p.id);
  const first = f.snapshot(g),
    second = { ...f.snapshot(h, false), distractions: [] };
  assert.equal((await f.upload(g.token, first)).status, 201);
  assert.equal((await f.upload(h.token, second)).status, 201);
  const db2 = new Store(f.file);
  try {
    const rec = db2.records();
    assert.equal(rec.sessions.length, 2);
    const s = rec.sessions.find((s) => s.id === g.sessionId)!;
    assert.equal(s.missions.length, 4);
    assert.equal(s.missions[0].timeSeconds, null);
    assert.equal(s.distractions.length, 9);
    assert.equal(s.distractions[0].count, 0);
    assert.equal(s.quiz?.percentage, 0);
    assert.equal(s.quiz?.incorrect, 3);
    assert.equal(rec.sessions.find((s) => s.id === h.sessionId)!.quiz, null);
  } finally {
    db2.db.close();
  }
  const records = await (
    await f.req("records", "GET", undefined, f.reader)
  ).json();
  assert.equal(
    records.sessions.filter(
      (s: { participantId: string }) => s.participantId === p.id,
    ).length,
    2,
  );
  const sum = await (await f.req("summary", "GET", undefined, f.reader)).json();
  assert.deepEqual(sum, {
    participants: 2,
    sessions: 2,
    completed: 1,
    averageDurationSeconds: 41.5,
    taskCompletionPercentage: 62.5,
    averageQuizPercentage: 0,
    recordedInteractions: 2,
  });
  const filtered = await (
    await f.req(
      "summary?mission=meeting&participant=" + p.id,
      "GET",
      undefined,
      f.admin,
    )
  ).json();
  assert.equal(filtered.sessions, 1);
  const emptyStats = await (
    await f.req("summary?to=2000-01-01", "GET", undefined, f.admin)
  ).json();
  assert.equal(emptyStats.averageQuizPercentage, null);
  assert.equal(emptyStats.recordedInteractions, null);
  for (const [type, lines] of Object.entries({
    sessions: 2,
    missions: 2,
    distractions: 10,
    quiz: 2,
  })) {
    const r = await f.req(
      "export?mission=meeting&type=" + type,
      "GET",
      undefined,
      f.admin,
    );
    const csv = await r.text();
    assert.equal(r.status, 200);
    // The mission dataset contains just the selected mission.
    assert.equal(csv.trimEnd().split("\r\n").length, lines);
    assert.ok(csv.includes("P001"));
    assert.ok(!csv.includes("Synthetic Fern"));
    assert.ok(!csv.includes(p.id));
  }
  assert.equal(
    (await f.req("export?from=2026-02-30", "GET", undefined, f.admin)).status,
    400,
  );
});

test("A failed child insert rolls back the session and leaves the binding available for retry", async (t) => {
  const f = await fixture(t);
  const participant = await f.add(),
    g = await f.bind(participant.id),
    body = f.snapshot(g);
  f.db.db.exec(
    "CREATE TRIGGER simulated_write_failure BEFORE INSERT ON quiz_results BEGIN SELECT RAISE(ABORT,'temporary test failure'); END",
  );
  assert.equal((await f.upload(g.token, body)).status, 500);
  for (const table of [
    "sessions",
    "mission_results",
    "distraction_results",
    "quiz_results",
  ])
    assert.equal(
      f.db.db.prepare("SELECT COUNT(*) n FROM " + table).get()!.n,
      0,
    );
  f.db.db.exec("DROP TRIGGER simulated_write_failure");
  assert.equal((await f.upload(g.token, body)).status, 201);
  f.db.prune(60, Date.now() + 8 * 86400000);
  assert.equal(
    f.db.db.prepare("SELECT COUNT(*) n FROM upload_grants").get()!.n,
    0,
  );
  assert.equal(
    f.db.db
      .prepare(
        "SELECT c.notice_version FROM sessions s JOIN consent_records c ON c.id=s.consent_record_id",
      )
      .get()!.notice_version,
    consent.noticeVersion,
  );
});

test("Offline redelivery, changed replay, malformed and unbound submissions never replace existing data", async (t) => {
  const f = await fixture(t);
  const p = await f.add(),
    g = await f.bind(p.id),
    other = await f.bind(p.id);
  const s = f.snapshot(g);
  assert.equal((await f.upload(other.token, s)).status, 403);
  assert.equal(
    (
      await f.upload(g.token, {
        ...s,
        quiz: { total: 3, answered: 3, correct: 4 },
      })
    ).status,
    400,
  );
  assert.equal(f.db.records().sessions.length, 0);
  // Send the same sealed packet twice, as when the original response was lost.
  const codes = await Promise.all([f.upload(g.token, s), f.upload(g.token, s)]);
  assert.deepEqual(codes.map((r) => r.status).sort(), [200, 201]);
  assert.equal(
    (await f.upload(g.token, { ...s, durationSeconds: 22 })).status,
    409,
  );
  assert.equal(f.db.records().sessions[0].durationSeconds, s.durationSeconds);
  const partial = {
    ...f.snapshot(other, false),
    missions: f
      .snapshot(other)
      .missions.map((m) => ({ ...m, completed: m.id !== "meeting" })),
    quiz: { total: 3, answered: 1, correct: 0 },
  };
  assert.equal((await f.upload(other.token, partial)).status, 201);
  const q = f.db.records().sessions.find((s) => s.id === other.sessionId)!.quiz;
  assert.equal(q?.percentage, null);
  assert.equal(q?.incorrect, 1);
  const opened = await f.bind(p.id);
  const unanswered = {
    ...partial,
    id: opened.sessionId,
    quiz: { total: 3, answered: 0, correct: 0 },
  };
  assert.equal((await f.upload(opened.token, unanswered)).status, 201);
  const observed = f.db
    .records()
    .sessions.find((s) => s.id === opened.sessionId)!.quiz;
  assert.equal(observed?.answered, 0);
  assert.equal(observed?.percentage, null);
  assert.equal(observed?.incorrect, 0);
});

test("Staff role boundaries, consent revocation and renewal, deletion, expiry and log out", async (t) => {
  const f = await fixture(t);
  for (const path of ["records", "summary", "export", "me"])
    assert.equal((await f.req(path)).status, 401);
  assert.equal((await f.req("submissions", "POST", {})).status, 401);
  assert.equal(
    (await f.req("participants", "POST", { consent: false }, f.admin)).status,
    400,
  );
  assert.equal(
    (await f.req("participants", "POST", consent, f.reader)).status,
    403,
  );
  assert.equal((await f.req("export", "GET", undefined, f.reader)).status, 403);
  assert.equal(
    (
      await f.req("participants", "POST", consent, f.admin, {
        Origin: "https://other.example",
      })
    ).status,
    403,
  );
  const p = await f.add(),
    g = await f.bind(p.id),
    path = "participants/" + p.id;
  assert.equal(
    (await f.req(path + "/binding", "POST", consent, f.reader)).status,
    403,
  );
  assert.equal((await f.req(path, "DELETE", undefined, f.reader)).status, 403);
  assert.equal(
    (await f.req(path + "/consent", "DELETE", undefined, f.admin)).status,
    204,
  );
  assert.equal((await f.upload(g.token, f.snapshot(g))).status, 403);
  assert.equal(
    (await f.req(path + "/binding", "POST", consent, f.admin)).status,
    409,
  );
  await f.req(path + "/consent", "POST", consent, f.admin);
  const active = await f.bind(p.id);
  assert.equal((await f.upload(g.token, f.snapshot(g))).status, 403);
  assert.equal((await f.upload(active.token, f.snapshot(active))).status, 201);
  assert.equal((await f.req(path, "DELETE", undefined, f.admin)).status, 204);
  assert.deepEqual(f.db.records(), { participants: [], sessions: [] });
  for (const table of [
    "quiz_results",
    "mission_results",
    "distraction_results",
    "upload_grants",
    "consent_records",
  ])
    assert.equal(
      f.db.db.prepare("SELECT COUNT(*) n FROM " + table).get()!.n,
      0,
    );
  const later = await f.add();
  assert.equal(later.code, "P002");
  const expired = await f.bind(later.id);
  f.db.db
    .prepare("UPDATE upload_grants SET expires_at=?")
    .run(new Date(0).toISOString());
  assert.equal(
    (await f.upload(expired.token, f.snapshot(expired))).status,
    403,
  );
  f.db.prune(60, Date.now() + 61 * 86400000);
  assert.equal(f.db.records().participants.length, 0);
  await f.req("logout", "POST", {}, f.admin);
  assert.equal((await f.req("records", "GET", undefined, f.admin)).status, 401);
});
