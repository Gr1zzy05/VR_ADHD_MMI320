import { Store } from "./store.ts";
import { start } from "./start.ts";
import { envInteger, settings } from "./config.ts";
const host = process.env.HOST ?? "127.0.0.1",
  port = envInteger("PORT", 4310, 1, 65535);
const url = new URL(process.env.PUBLIC_ORIGIN ?? "http://127.0.0.1:" + port);
if (
  url.pathname !== "/" ||
  url.search ||
  url.hash ||
  url.username ||
  url.password
)
  throw new Error("PUBLIC_ORIGIN must be an origin");
if (
  url.protocol !== "https:" &&
  !(
    url.protocol === "http:" &&
    ["127.0.0.1", "localhost", "[::1]"].includes(url.hostname) &&
    ["127.0.0.1", "::1", "localhost"].includes(host)
  )
)
  throw new Error("Use HTTPS at a reverse proxy for network access");
const config = settings(url.origin);
start(
  new Store(process.env.DB_PATH ?? "data/analytics.sqlite"),
  config,
  host,
  port,
);
