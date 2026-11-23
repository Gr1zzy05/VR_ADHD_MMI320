import test from "node:test";
import assert from "node:assert/strict";
import { PGlite } from "@electric-sql/pglite";
import { PostgresStore } from "../server/postgres/store.ts";
import { missionIds } from "../server/model.ts";
import { application } from "../server/http.ts";
import { once } from "node:events";
import { randomUUID } from "node:crypto";
import type { AddressInfo } from "node:net";
import type { Transaction } from "../server/postgres/store.ts";

test("Hosted API shares PostgreSQL data and auth across two web instances", async (t) => {
  const db = new PGlite();
  t.after(() => db.close());
  const transaction: Transaction = (work) =>
    db.transaction((tx) =>
      work({
        query: (sql, values) => tx.query(sql, values),
        executeScript: (sql) => tx.exec(sql),
      }),
    );
  const one = new PostgresStore(db, transaction);
  const two = new PostgresStore(db, transaction);
  await one.migrate();
  await two.migrate();
  const config = {
    origin: "https://focus-shift.example",
    demo: false,
    retentionDays: 180,
    grantDays: 7,
    adminUser: "admin",
    adminPassword: "temporary-admin-secret",
    educatorUser: "educator",
    educatorPassword: "temporary-educator-secret",
    cronSecret: "temporary-cron-secret",
    enrollmentKey: "a".repeat(43),
  };
  async function web(store: PostgresStore) {
    const { app, close } = application(store, config, store);
    const listener = app.listen(0, "127.0.0.1");
    await once(listener, "listening");
    t.after(async () => {
      await new Promise<void>((resolve, reject) =>
        listener.close((err) => (err ? reject(err) : resolve())),
      );
      close();
    });
    return (
      path: string,
      method = "GET",
      body?: unknown,
      headers?: Record<string, string>,
    ) =>
      fetch(
        "http://127.0.0.1:" + (listener.address() as AddressInfo).port + path,
        {
          method,
          headers: { "Content-Type": "application/json", ...headers },
          body: body === undefined ? undefined : JSON.stringify(body),
        },
      );
  }
  const a = await web(one),
    b = await web(two);
  const consent = { consent: true, noticeVersion: "2026-01" };
  const login = await a("/api/login", "POST", {
    username: "admin",
    password: config.adminPassword,
  });
  assert.equal(login.status, 200);
  const cookie = login.headers.get("set-cookie")!;
  assert.match(cookie, /Secure/);
  const auth = { Cookie: cookie.split(";")[0] };
  const me = await b("/api/me", "GET", undefined, auth);
  assert.equal((await me.json()).role, "admin");
  assert.equal((await a("/api/records")).status, 401);
  const enrollment = await b("/api/participants", "POST", consent, auth);
  assert.equal(enrollment.status, 201);
  const { id, code } = await enrollment.json();
  assert.equal(code, "P001");
  const endpoint = "/api/participants/" + id + "/pair";
  const begin = await a("/api/game/pair", "POST", {});
  assert.equal(begin.status, 201);
  const pairing = await begin.json();
  assert.match(pairing.code, /^[0-9A-F]{8}$/);
  const poll = "/api/game/pair/" + pairing.code;
  const gameAuth = { Authorization: "Bearer " + pairing.secret };
  assert.equal((await b(poll, "GET", undefined, gameAuth)).status, 204);
  assert.equal((await b(poll)).status, 401);
  const approval = { ...consent, code: pairing.code, runs: 2 };
  assert.equal(
    (await b(endpoint, "POST", { ...approval, runs: 31 }, auth)).status,
    400,
  );
  assert.equal((await b(endpoint, "POST", approval)).status, 401);
  assert.equal((await a(endpoint, "POST", approval, auth)).status, 204);
  const binding = await b(poll, "GET", undefined, gameAuth);
  assert.equal(binding.status, 200);
  const pack = await binding.json();
  assert.deepEqual(
    await (await a(poll, "GET", undefined, gameAuth)).json(),
    pack,
  );
  assert.equal(
    (
      await b(poll, "GET", undefined, {
        Authorization: "Bearer " + "a".repeat(43),
      })
    ).status,
    404,
  );
  assert.equal((await a(endpoint, "POST", approval, auth)).status, 409);
  const persistedPair = (
    await db.query<{ sealed_pack: string; secret_hash: string }>(
      "SELECT sealed_pack,secret_hash FROM game_pairings",
    )
  ).rows[0];
  assert.ok(!persistedPair.sealed_pack.includes(pack.bindings[0].token));
  assert.notEqual(persistedPair.secret_hash, pairing.secret);
  assert.equal(pack.participantCode, "P001");
  const [grant, next] = pack.bindings;
  assert.notEqual(grant.sessionId, next.sessionId);
  assert.notEqual(grant.token, next.token);
  assert.equal(next.participantId, id);
  assert.equal(grant.endpoint, config.origin + "/api/submissions");
  const now = new Date().toISOString();
  const payload = {
    id: grant.sessionId,
    participantId: id,
    startedAt: now,
    endedAt: now,
    durationSeconds: 0,
    status: "interrupted",
    gameVersion: "test",
    missions: missionIds.map((mission) => ({ id: mission, completed: false })),
    distractions: [{ kind: "Phone", count: 0, seconds: 0 }],
    quiz: { total: 3, answered: 0, correct: 0 },
  };
  const device = { Authorization: "Bearer " + grant.token };
  assert.equal(
    (await b("/api/submissions", "POST", payload, device)).status,
    201,
  );
  assert.equal(
    (await a("/api/submissions", "POST", payload, device)).status,
    200,
  );
  const nextPayload = { ...payload, id: next.sessionId };
  assert.equal(
    (await a("/api/submissions", "POST", nextPayload, device)).status,
    403,
  );
  assert.equal(
    (
      await a("/api/submissions", "POST", nextPayload, {
        Authorization: "Bearer " + next.token,
      })
    ).status,
    201,
  );
  const collected = await a("/api/records", "GET", undefined, auth);
  assert.equal(collected.status, 200);
  const records = await collected.json();
  assert.equal(records.sessions.length, 2);
  assert.ok(
    records.sessions.every(
      (session: { participantId: string }) => session.participantId === id,
    ),
  );
  assert.equal(records.sessions[0].durationSeconds, 0);
  assert.equal(records.sessions[0].quiz.answered, 0);
  assert.equal(records.sessions[0].quiz.percentage, null);
  assert.equal(records.sessions[0].missions.length, 4);
  assert.equal(records.sessions[0].missions[0].timeSeconds, null);
  const csv = await b("/api/export?type=quiz", "GET", undefined, auth);
  assert.equal(csv.status, 200);
  assert.match(await csv.text(), /P001/);
  assert.equal((await a("/api/retention")).status, 401);
  assert.equal(
    (
      await a("/api/retention", "GET", undefined, {
        Authorization: "Bearer " + config.cronSecret,
      })
    ).status,
    204,
  );
  assert.equal(
    (await a("/api/participants/" + id + "/consent", "DELETE", undefined, auth))
      .status,
    204,
  );
  assert.equal(
    (await b("/api/submissions", "POST", payload, device)).status,
    403,
  );
  assert.equal((await b(poll, "GET", undefined, gameAuth)).status, 404);
  const automaticStart = "/api/game/start";
  const build = { Authorization: "Bearer " + config.enrollmentKey };
  const firstStart = { ...consent, requestId: randomUUID() };
  assert.equal((await a(automaticStart, "POST", firstStart)).status, 403);
  assert.equal(
    (await a(automaticStart, "POST", { ...firstStart, consent: false }, build))
      .status,
    400,
  );
  assert.equal((await one.records()).participants.length, 1);
  const firstResponse = await a(automaticStart, "POST", firstStart, build);
  assert.equal(firstResponse.status, 200);
  const firstPlayer = await firstResponse.json();
  assert.deepEqual(
    await (await b(automaticStart, "POST", firstStart, build)).json(),
    firstPlayer,
  );
  const nextResponse = await b(
    automaticStart,
    "POST",
    { ...consent, requestId: randomUUID() },
    build,
  );
  assert.equal(nextResponse.status, 200);
  const nextPlayer = await nextResponse.json();
  assert.equal(firstPlayer.participantCode, "P002");
  assert.equal(nextPlayer.participantCode, "P003");
  assert.equal(firstPlayer.bindings.length, 1);
  assert.equal(nextPlayer.bindings.length, 1);
  assert.notEqual(
    firstPlayer.bindings[0].participantId,
    nextPlayer.bindings[0].participantId,
  );
  const firstTicket = firstPlayer.bindings[0];
  const round = {
    ...payload,
    id: firstTicket.sessionId,
    participantId: firstTicket.participantId,
    startedAt: new Date().toISOString(),
    endedAt: new Date().toISOString(),
  };
  assert.equal(
    (
      await b("/api/submissions", "POST", round, {
        Authorization: "Bearer " + firstTicket.token,
      })
    ).status,
    201,
  );
  assert.equal(
    (await one.records()).sessions.find((s) => s.id === round.id)
      ?.participantId,
    firstTicket.participantId,
  );
  assert.equal(
    (
      await a(
        "/api/participants/" + firstTicket.participantId + "/consent",
        "DELETE",
        undefined,
        auth,
      )
    ).status,
    204,
  );
  assert.equal(
    (await b(automaticStart, "POST", firstStart, build)).status,
    403,
  );
  assert.equal((await b("/api/logout", "POST", {}, auth)).status, 204);
  assert.equal((await a("/api/me", "GET", undefined, auth)).status, 401);
});
