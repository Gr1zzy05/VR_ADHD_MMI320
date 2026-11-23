import type { Settings } from "./http.ts";

function invalid(code: string, message: string): never {
  throw Object.assign(new Error(message), { code });
}

export function envInteger(
  key: string,
  fallback: number,
  min: number,
  max: number,
) {
  const n = Number(process.env[key] ?? fallback);
  if (!Number.isSafeInteger(n) || n < min || n > max)
    invalid("INVALID_" + key, "Invalid " + key);
  return n;
}
function secret(key: string) {
  const value = process.env[key];
  if (!value || value.length < 16 || value.length > 512)
    invalid("INVALID_" + key, "Configure " + key + " with 16–512 characters");
  return value;
}
export function settings(origin: string, hosted = false): Settings {
  const adminUser = process.env.ADMIN_USER ?? "admin";
  const educatorUser = process.env.EDUCATOR_USER ?? "educator";
  const adminPassword = secret("ADMIN_PASSWORD");
  const educatorPassword = secret("EDUCATOR_PASSWORD");
  const enrollmentKey = process.env.GAME_ENROLLMENT_KEY || undefined;
  if (enrollmentKey && !/^[A-Za-z0-9_-]{43}$/.test(enrollmentKey))
    invalid("INVALID_GAME_ENROLLMENT_KEY", "GAME_ENROLLMENT_KEY requires 43 base64url characters");
  if (
    !adminUser.trim() ||
    !educatorUser.trim() ||
    adminUser.length > 100 ||
    educatorUser.length > 100 ||
    adminUser === educatorUser ||
    adminPassword === educatorPassword
  )
    invalid("INVALID_OR_DUPLICATE_STAFF_CREDENTIALS", "Configure distinct staff credentials");
  const cronSecret = hosted ? secret("CRON_SECRET") : undefined;
  if (cronSecret && !/^[A-Za-z0-9_-]+$/.test(cronSecret))
    invalid("INVALID_CRON_SECRET", "Invalid CRON_SECRET");
  return {
    origin,
    adminUser,
    adminPassword,
    educatorUser,
    educatorPassword,
    enrollmentKey,
    cronSecret,
    demo: false,
    retentionDays: envInteger("RETENTION_DAYS", 180, 1, 3650),
    grantDays: envInteger("GRANT_DAYS", 7, 1, 30),
  };
}
