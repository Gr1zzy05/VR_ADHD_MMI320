import { Store } from "./store.ts";
import { start } from "./start.ts";
import { missionIds, kinds } from "./model.ts";

// A separate local database. This executable deliberately ignores deployment credentials and DB_PATH.
const port = Number(process.env.DEMO_PORT ?? 4311),
  origin = "http://127.0.0.1:" + port;
const store = new Store("data/demo.synthetic.sqlite");
store.db.exec(
  "CREATE TABLE IF NOT EXISTS demo_seed (version INTEGER PRIMARY KEY) STRICT",
);
if (!store.db.prepare("SELECT 1 FROM demo_seed").get()) {
  const now = Date.now();
  const iso = (t: number) => new Date(t).toISOString();
  const names = [
    "Fern",
    "Sky",
    null,
    "Mali",
    null,
    "River",
    "Mint",
    null,
    "Blue",
    null,
    "Pim",
    "Oak",
    null,
    "Dao",
    "Rain",
    null,
    "Sun",
    "Nok",
  ];
  const peers = names.map((n) => store.register(n));
  const issued = iso(now - 45 * 86400000);
  store.db.prepare("UPDATE participants SET registered_at=?").run(issued);
  store.db.prepare("UPDATE consent_records SET granted_at=?").run(issued);
  for (let i = 0; i < 54; i++) {
    const p = peers[i % peers.length];
    const g = store.bind(p.id, origin + "/api/submissions", 7);
    const t = now - ((53 - i) * 0.49 + 0.02) * 86400000;
    store.db
      .prepare("UPDATE upload_grants SET issued_at=? WHERE session_id=?")
      .run(issued, g.sessionId);
    const finished = i % 9 !== 2,
      done = finished ? 4 : i % 3;
    const durationSeconds = finished
      ? 380 + ((i * 59) % 315)
      : 96 + ((i * 17) % 120);
    const missions = missionIds.map((id, j) => {
      const completed = j < done;
      if (id === "meeting") return { id, completed };
      const itemsTotal = j === 2 ? 3 : 6;
      return {
        id,
        completed,
        itemsTotal,
        itemsDone: completed ? itemsTotal : j === done ? 1 : 0,
      };
    });
    const distractions = kinds.map((kind, j) => {
      const count = (i * (j + 1) + j * 7) % (j === 5 || j === 3 ? 8 : 4);
      return { kind, count, seconds: count * (3 + ((i + j) % 9)) };
    });
    store.record(
      g.token,
      {
        id: g.sessionId,
        participantId: p.id,
        startedAt: iso(t),
        endedAt: iso(t + durationSeconds * 1000),
        durationSeconds,
        gameVersion: "1.0-demo",
        status: finished ? "completed" : "interrupted",
        missions,
        distractions,
        quiz: finished
          ? {
              total: 3,
              answered: 3,
              correct: i % 5 === 0 ? 1 : i % 3 === 0 ? 2 : 3,
            }
          : null,
      },
      180,
    );
  }
  store.db.exec("DELETE FROM upload_grants; INSERT INTO demo_seed VALUES(1)");
}
start(
  store,
  {
    origin,
    demo: true,
    retentionDays: 180,
    grantDays: 7,
    adminUser: "demo-admin",
    educatorUser: "demo-educator",
    adminPassword: "focus-shift-demo",
    educatorPassword: "focus-shift-demo",
  },
  "127.0.0.1",
  port,
);
