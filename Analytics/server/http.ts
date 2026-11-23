import express from "express";
import type { Request, Response, NextFunction } from "express";
import { randomBytes, scryptSync, timingSafeEqual } from "node:crypto";
import { fileURLToPath } from "node:url";
import {
  ApiError,
  check,
  digest,
  fields,
  noticeVersion,
  uuid,
} from "./model.ts";
import { filtered, csv, summary } from "./reports.ts";
import type { AnalyticsStore } from "./store.ts";
import type { StaffSessions } from "./postgres/store.ts";
import { protectDevicePacks } from "./bindings.ts";

export type Settings = {
  origin: string;
  retentionDays: number;
  grantDays: number;
  demo: boolean;
  adminUser: string;
  adminPassword: string;
  educatorUser: string;
  educatorPassword: string;
  cronSecret?: string;
  enrollmentKey?: string;
};
type Role = "admin" | "educator";
export function application(
  store: AnalyticsStore,
  config: Settings,
  cloudAuth?: StaffSessions,
) {
  const app = express();
  app.disable("x-powered-by");
  const users = [
    ["admin", config.adminUser, config.adminPassword],
    ["educator", config.educatorUser, config.educatorPassword],
  ].map(([role, name, pw]) => {
    const salt = randomBytes(16);
    return {
      role: role as Role,
      name,
      salt,
      password: scryptSync(pw, salt, 32),
    };
  });
  const logins = new Map<string, { role: Role; expires: number }>();
  const attempts = new Map<string, { count: number; until: number }>();
  const credentialVersion = cloudAuth
    ? digest(
        JSON.stringify([
          config.adminUser,
          config.adminPassword,
          config.educatorUser,
          config.educatorPassword,
        ]),
      )
    : "";
  const https = new URL(config.origin).protocol === "https:";
  const devicePacks = https ? protectDevicePacks(config.adminPassword) : null;
  const enrollmentAuth =
    https && config.enrollmentKey
      ? Buffer.from(digest("Bearer " + config.enrollmentKey), "hex")
      : null;
  const publicConfig = {
    demo: config.demo,
    retentionDays: config.retentionDays,
    grantDays: config.grantDays,
    canProvision: https,
  };
  // The local demo keeps logins in memory. Hosted instances share PostgreSQL.
  function clean() {
    for (const [k, s] of logins) if (s.expires <= Date.now()) logins.delete(k);
    for (const [k, s] of attempts)
      if (s.until <= Date.now()) attempts.delete(k);
  }
  const upkeep = cloudAuth ? undefined : setInterval(clean, 60000);
  upkeep?.unref();
  async function rate(
    req: Request,
    res: Response,
    scope: string,
    max: number,
    ms: number,
  ) {
    const address = cloudAuth
      ? req.get("x-vercel-forwarded-for")?.split(",")[0].trim() ||
        req.socket.remoteAddress
      : req.socket.remoteAddress;
    const key = digest(scope + "-" + address);
    if (cloudAuth) {
      const limit = await cloudAuth.rate(key, Date.now(), ms);
      if (limit.count > max) {
        res.set(
          "Retry-After",
          String(Math.max(1, Math.ceil((limit.until - Date.now()) / 1000))),
        );
        throw new ApiError(429, "Try again later");
      }
      return;
    }
    let s = attempts.get(key);
    if (!s || s.until <= Date.now()) {
      s = { count: 0, until: Date.now() + ms };
      attempts.set(key, s);
    }
    if (++s.count > max) {
      res.set("Retry-After", String(Math.ceil((s.until - Date.now()) / 1000)));
      throw new ApiError(429, "Try again later");
    }
  }
  function key(req: Request) {
    const token = (req.headers.cookie ?? "")
      .split(";")
      .map((s) => s.trim())
      .find((s) => s.startsWith("fs_session="))
      ?.slice(11);
    return /^[A-Za-z0-9_-]{43}$/.test(token ?? "")
      ? digest(credentialVersion + token)
      : "";
  }
  async function role(req: Request): Promise<Role> {
    const token = key(req);
    if (!token) throw new ApiError(401, "Staff sign-in required");
    if (cloudAuth) {
      const stored = await cloudAuth.find(token, Date.now());
      if (!stored) throw new ApiError(401, "Staff sign-in required");
      return stored;
    }
    const s = logins.get(token);
    if (!s || s.expires <= Date.now())
      throw new ApiError(401, "Staff sign-in required");
    return s.role;
  }
  async function admin(req: Request) {
    if ((await role(req)) !== "admin")
      throw new ApiError(403, "Administrator access required");
  }
  function consent(body: Record<string, unknown>) {
    check(
      body.consent === true && body.noticeVersion === noticeVersion,
      "Confirm the current participant notice",
    );
  }
  function pairingCode(value: unknown) {
    check(
      typeof value === "string" && /^[0-9A-F]{8}$/.test(value),
      "Enter the 8-character code shown in the game",
    );
    return value;
  }
  function preparedRuns(value: unknown) {
    check(
      typeof value === "number" &&
        Number.isInteger(value) &&
        value >= 1 &&
        value <= 30,
      "Choose between 1 and 30 sessions",
    );
    return value;
  }
  function requirePairing() {
    if (!devicePacks)
      throw new ApiError(
        409,
        "Configure a trusted HTTPS public origin before pairing",
      );
    return devicePacks;
  }
  app.use((req, res, next) => {
    res.set({
      "X-Content-Type-Options": "nosniff",
      "Referrer-Policy": "no-referrer",
      "X-Frame-Options": "DENY",
      "Content-Security-Policy":
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'none'; form-action 'self'",
    });
    if (https) res.set("Strict-Transport-Security", "max-age=31536000");
    if (req.path.startsWith("/api/")) res.set("Cache-Control", "no-store");
    if (
      config.demo &&
      !["127.0.0.1", "localhost", "[::1]"].includes(req.hostname)
    )
      throw new ApiError(403, "Demo is loopback only");
    if (!["GET", "HEAD", "OPTIONS"].includes(req.method)) {
      if (req.headers.origin && req.headers.origin !== config.origin)
        throw new ApiError(403, "Cross-origin request denied");
      if (req.headers["sec-fetch-site"] === "cross-site")
        throw new ApiError(403, "Cross-site request denied");
      if (req.method === "POST" && !req.is("application/json"))
        throw new ApiError(415, "Expected application/json");
    }
    next();
  });
  app.use("/api", express.json({ limit: "32kb", inflate: false }));
  app.get("/api/config", (_req, res) => res.json(publicConfig));
  app.post("/api/login", async (req, res) => {
    await rate(req, res, "login", 20, 300000);
    const { username, password } = fields(req.body, ["username", "password"]);
    check(
      typeof username === "string" &&
        username.length <= 100 &&
        typeof password === "string" &&
        password.length <= 512,
      "Invalid login",
    );
    const user = users.find((u) => u.name === username);
    const reference = user ?? users[0];
    const ok = timingSafeEqual(
      scryptSync(password, reference.salt, 32),
      reference.password,
    );
    if (!user || !ok) throw new ApiError(401, "Invalid staff credentials");
    const previous = key(req);
    const session = randomBytes(32).toString("base64url");
    const sessionHash = digest(credentialVersion + session);
    const expires = Date.now() + 8 * 3600000;
    if (cloudAuth) {
      if (previous) await cloudAuth.removeStaff(previous);
      await cloudAuth.save(sessionHash, user.role, expires);
    } else {
      logins.delete(previous);
      logins.set(sessionHash, { role: user.role, expires });
    }
    res
      .cookie("fs_session", session, {
        httpOnly: true,
        secure: https,
        sameSite: "strict",
        path: "/",
        maxAge: 8 * 3600000,
      })
      .json({ role: user.role, ...publicConfig });
  });
  app.post("/api/logout", async (req, res) => {
    const token = key(req);
    if (cloudAuth && token) await cloudAuth.removeStaff(token);
    else logins.delete(token);
    res
      .clearCookie("fs_session", {
        path: "/",
        secure: https,
        httpOnly: true,
        sameSite: "strict",
      })
      .sendStatus(204);
  });
  app.get("/api/me", async (req, res) =>
    res.json({ role: await role(req), ...publicConfig }),
  );
  app.get("/api/records", async (req, res) => {
    await role(req);
    res.json(await store.records());
  });
  app.get("/api/summary", async (req, res) => {
    await role(req);
    res.json(
      summary(filtered(await store.records(), req.query), req.query.mission),
    );
  });
  app.get("/api/export", async (req, res) => {
    await admin(req);
    const type = req.query.type ?? "sessions";
    const content = csv(
      filtered(await store.records(), req.query),
      type,
      req.query.mission,
    );
    res
      .attachment("focus-shift-" + type + ".csv")
      .type("text/csv; charset=utf-8")
      .send(content);
  });
  app.post("/api/participants", async (req, res) => {
    await admin(req);
    const b = fields(req.body, ["nickname", "noticeVersion", "consent"]);
    consent(b);
    check(
      b.nickname == null ||
        (typeof b.nickname === "string" &&
          b.nickname.length <= 60 &&
          !/[\x00-\x1f]/.test(b.nickname)),
      "Invalid nickname",
    );
    res
      .status(201)
      .json(
        await store.register(
          typeof b.nickname === "string" ? b.nickname.trim() || null : null,
        ),
      );
  });
  app.post("/api/participants/reset-codes", async (req, res) => {
    await admin(req);
    const body = fields(req.body, ["confirmation"]);
    check(body.confirmation === "P001", "Confirm the reset with P001");
    await store.resetPlayerCodes();
    res.sendStatus(204);
  });
  app.post("/api/participants/:id/consent", async (req, res) => {
    await admin(req);
    consent(fields(req.body, ["noticeVersion", "consent"]));
    await store.consent(uuid(req.params.id), true);
    res.sendStatus(204);
  });
  app.delete("/api/participants/:id/consent", async (req, res) => {
    await admin(req);
    await store.consent(uuid(req.params.id), false);
    res.sendStatus(204);
  });
  app.delete("/api/participants/:id", async (req, res) => {
    await admin(req);
    await store.remove(uuid(req.params.id));
    res.sendStatus(204);
  });
  app.post("/api/participants/:id/binding", async (req, res) => {
    await admin(req);
    consent(fields(req.body, ["noticeVersion", "consent"]));
    if (!https)
      throw new ApiError(
        409,
        "Configure a trusted HTTPS public origin before provisioning a headset",
      );
    res
      .attachment("focus-shift-binding.json")
      .json(
        await store.bind(
          uuid(req.params.id),
          config.origin + "/api/submissions",
          config.grantDays,
        ),
      );
  });
  app.post("/api/participants/:id/device", async (req, res) => {
    await admin(req);
    const body = fields(req.body, ["noticeVersion", "consent", "runs"]);
    consent(body);
    if (!https)
      throw new ApiError(
        409,
        "Configure a trusted HTTPS public origin before provisioning a headset",
      );
    const runs = preparedRuns(body.runs);
    const pack = await store.bindMany(
      uuid(req.params.id),
      config.origin + "/api/submissions",
      config.grantDays,
      runs,
    );
    res.attachment("focus-shift-device.json").json(pack);
  });
  app.post("/api/game/pair", async (req, res) => {
    requirePairing();
    await rate(req, res, "pair-start", 30, 60000);
    fields(req.body, []);
    const secret = randomBytes(32).toString("base64url");
    const now = Date.now();
    const expiresAt = new Date(now + 3 * 60000).toISOString();
    for (let attempt = 0; attempt < 3; attempt++) {
      const code = randomBytes(4).toString("hex").toUpperCase();
      if (
        await store.openPairing(
          code,
          digest(secret),
          new Date(now).toISOString(),
          expiresAt,
        )
      ) {
        res.status(201).json({ code, secret, expiresAt });
        return;
      }
    }
    throw new ApiError(503, "Could not start pairing");
  });
  app.post("/api/game/start", async (req, res) => {
    if (!enrollmentAuth)
      throw new ApiError(404, "Automatic enrollment is disabled");
    const supplied = Buffer.from(digest(req.get("Authorization") ?? ""), "hex");
    if (!timingSafeEqual(enrollmentAuth, supplied))
      throw new ApiError(403, "Game enrollment credential required");
    await rate(req, res, "game-start", 30, 60000);
    const body = fields(req.body, ["requestId", "noticeVersion", "consent"]);
    consent(body);
    const requestHash = digest("game-start:" + uuid(body.requestId));
    const now = Date.now();
    const protection = requirePairing();
    const sealed = await store.enrollPlayer(
      requestHash,
      config.origin + "/api/submissions",
      config.grantDays,
      new Date(now).toISOString(),
      new Date(now + 15 * 60000).toISOString(),
      (pack) => protection.seal(pack, requestHash),
    );
    res.json(protection.open(sealed, requestHash));
  });
  app.get("/api/game/pair/:code", async (req, res) => {
    const protection = requirePairing();
    await rate(req, res, "pair-poll", 180, 60000);
    const code = pairingCode(req.params.code);
    const secret = /^Bearer ([A-Za-z0-9_-]{43})$/.exec(
      req.get("Authorization") ?? "",
    )?.[1];
    if (!secret) throw new ApiError(401, "Device secret required");
    const sealed = await store.pairedPack(
      code,
      digest(secret),
      new Date().toISOString(),
    );
    if (sealed === null) {
      res.sendStatus(204);
      return;
    }
    res.json(protection.open(sealed, code));
  });
  app.post("/api/participants/:id/pair", async (req, res) => {
    await admin(req);
    const protection = requirePairing();
    const body = fields(req.body, ["noticeVersion", "consent", "runs", "code"]);
    consent(body);
    const code = pairingCode(body.code);
    await store.approvePairing(
      code,
      uuid(req.params.id),
      config.origin + "/api/submissions",
      config.grantDays,
      preparedRuns(body.runs),
      (pack) => protection.seal(pack, code),
    );
    res.sendStatus(204);
  });
  app.post("/api/submissions", async (req, res) => {
    await rate(req, res, "ingest", 240, 60000);
    const token = /^Bearer ([A-Za-z0-9_-]{43})$/.exec(
      req.get("Authorization") ?? "",
    )?.[1];
    if (!token) throw new ApiError(401, "Session credential required");
    const saved = await store.record(token, req.body, config.retentionDays);
    res.status(saved ? 201 : 200).json({ saved: true, id: req.body.id });
  });
  if (config.cronSecret) {
    const expected = Buffer.from(digest("Bearer " + config.cronSecret), "hex");
    app.get("/api/retention", async (req, res) => {
      const presented = Buffer.from(
        digest(req.get("Authorization") ?? ""),
        "hex",
      );
      if (!timingSafeEqual(expected, presented))
        throw new ApiError(401, "Maintenance authorization required");
      await store.prune(config.retentionDays);
      res.sendStatus(204);
    });
  }
  app.use("/api", (_req, res) =>
    res.status(404).json({ error: "Unknown API path" }),
  );
  const web = fileURLToPath(new URL("../dist/", import.meta.url));
  app.use(express.static(web, { index: "index.html" }));
  app.get("/{*path}", (req, res, next) => {
    if (!req.accepts("html") || req.path.includes(".")) return next();
    res.sendFile("index.html", { root: web });
  });
  app.use(
    (
      err: Error & { status?: number },
      _req: Request,
      res: Response,
      _next: NextFunction,
    ) => {
      const status =
        err instanceof ApiError
          ? err.status
          : err.status && err.status >= 400 && err.status < 500
            ? err.status
            : 500;
      res.status(status).json({
        error:
          err instanceof ApiError
            ? err.message
            : status < 500
              ? "Invalid request"
              : "Server error",
      });
    },
  );
  return {
    app,
    close: () => {
      clearInterval(upkeep);
      logins.clear();
      attempts.clear();
    },
  };
}
