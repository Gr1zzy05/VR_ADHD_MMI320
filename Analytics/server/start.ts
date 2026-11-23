import { Store } from "./store.ts";
import { application } from "./http.ts";
import type { Settings } from "./http.ts";

export function start(
  store: Store,
  settings: Settings,
  host: string,
  port: number,
) {
  store.prune(settings.retentionDays);
  const timer = setInterval(() => store.prune(settings.retentionDays), 3600000);
  timer.unref();
  const { app, close } = application(store, settings);
  const server = app.listen(port, host, (error?: Error) => {
    if (error) {
      console.error("Could not start FOCUS SHIFT:", error.message);
      clearInterval(timer);
      close();
      store.db.close();
      process.exitCode = 1;
      return;
    }
    console.log("FOCUS SHIFT is ready at " + settings.origin);
  });
  server.headersTimeout = 15000;
  server.requestTimeout = 30000;
  function shutdown() {
    clearInterval(timer);
    server.close(() => {
      close();
      store.db.close();
    });
  }
  process.once("SIGTERM", shutdown);
  process.once("SIGINT", shutdown);
}
