import type { IncomingMessage, ServerResponse } from "node:http";
import pg from "pg";
import { application } from "../server/http.ts";
import { settings } from "../server/config.ts";
import { PostgresStore } from "../server/postgres/store.ts";
import type { Transaction } from "../server/postgres/store.ts";

async function initialize() {
  let stage = "DATABASE_URL";
  try {
    return await initializeService(value => { stage = value; });
  } catch (error) {
    const code = error && typeof error === "object" && "code" in error &&
      typeof error.code === "string" && /^[A-Z0-9_]{1,40}$/.test(error.code)
      ? error.code : "UNAVAILABLE";
    console.error("Focus Shift API startup failed at " + stage + " (" + code + ")");
    throw error;
  }
}

async function initializeService(markStage: (stage: string) => void) {
  const databaseUrl = process.env.DATABASE_URL;
  if (!databaseUrl) throw new Error("Configure DATABASE_URL");
  const database = new URL(databaseUrl);
  if (!["postgresql:", "postgres:"].includes(database.protocol))
    throw new Error("DATABASE_URL must be a PostgreSQL URL");
  database.searchParams.set("sslmode", "verify-full");
  markStage("PUBLIC_ORIGIN");
  const hostedDomain =
    process.env.VERCEL_ENV === "production"
      ? process.env.VERCEL_PROJECT_PRODUCTION_URL
      : process.env.VERCEL_URL;
  const origin = new URL(
    process.env.PUBLIC_ORIGIN ??
      (hostedDomain ? "https://" + hostedDomain : ""),
  );
  if (
    origin.protocol !== "https:" ||
    origin.pathname !== "/" ||
    origin.search ||
    origin.hash ||
    origin.username ||
    origin.password
  )
    throw new Error("PUBLIC_ORIGIN must be an HTTPS origin");
  markStage("staff/cron/game environment variables");
  const config = settings(origin.origin, true);
  const pool = new pg.Pool({
    connectionString: database.toString(),
    max: 3,
    idleTimeoutMillis: 5000,
    connectionTimeoutMillis: 5000,
    allowExitOnIdle: true,
    enableChannelBinding: true,
  });
  pool.on("error", () => console.error("Idle PostgreSQL connection closed"));
  const transaction: Transaction = async (work) => {
    const client = await pool.connect();
    try {
      await client.query("BEGIN");
      const result = await work({
        query: (sql, values) => client.query(sql, values),
        executeScript: (sql) => client.query(sql),
      });
      await client.query("COMMIT");
      return result;
    } catch (error) {
      await client.query("ROLLBACK");
      throw error;
    } finally {
      client.release();
    }
  };
  const store = new PostgresStore(pool, transaction);
  try {
    markStage("PostgreSQL connection or migrations");
    await store.migrate();
    return application(store, config, store).app;
  } catch (error) {
    await pool.end();
    throw error;
  }
}
let ready: ReturnType<typeof initialize> | undefined;

export default async function handler(
  req: IncomingMessage,
  res: ServerResponse,
) {
  const url = new URL(req.url ?? "/", "http://localhost");
  const route = url.searchParams.get("_focusRoute");
  if (url.pathname === "/api/router") {
    if (!route || !/^[a-zA-Z0-9/-]+$/.test(route) || route === "router") {
      res.writeHead(404, {
        "Content-Type": "application/json",
        "Cache-Control": "no-store",
      });
      res.end('{"error":"Unknown API path"}');
      return;
    }
    url.pathname = "/api/" + route;
  }
  url.searchParams.delete("_focusRoute");
  req.url = url.pathname + url.search;
  ready ??= initialize().catch((error) => {
    ready = undefined;
    throw error;
  });
  try {
    const app = await ready;
    app(req, res);
  } catch {
    res.writeHead(503, {
      "Content-Type": "application/json",
      "Cache-Control": "no-store",
    });
    res.end('{"error":"Workspace service unavailable"}');
  }
}
