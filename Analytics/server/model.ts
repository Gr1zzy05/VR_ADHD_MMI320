import { createHash } from "node:crypto";

export const missionIds = [
  "washing",
  "organizing",
  "trash",
  "meeting",
] as const;
export const kinds = [
  "FidgetCube",
  "PopIt",
  "Squishy",
  "Television",
  "Plushie",
  "Phone",
  "Conversation",
  "Party",
  "Construction",
] as const;
export const noticeVersion = "2026-01";
export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}
export function check(condition: unknown, message: string): asserts condition {
  if (!condition) throw new ApiError(400, message);
}
export function fields(
  value: unknown,
  allowed: string[],
): Record<string, unknown> {
  check(
    typeof value === "object" && value !== null && !Array.isArray(value),
    "Expected a JSON object",
  );
  check(
    Object.keys(value).every((key) => allowed.includes(key)),
    "Unexpected field",
  );
  return value as Record<string, unknown>;
}
export function uuid(v: unknown): string {
  check(
    typeof v === "string" &&
      /^[a-f\d]{8}-[a-f\d]{4}-4[a-f\d]{3}-[89ab][a-f\d]{3}-[a-f\d]{12}$/i.test(
        v,
      ),
    "Expected a version 4 UUID",
  );
  return v.toLowerCase();
}
function instant(v: unknown) {
  check(
    typeof v === "string" &&
      /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?Z$/.test(v) &&
      Number.isFinite(Date.parse(v)),
    "Expected a UTC timestamp",
  );
  const iso = new Date(v).toISOString();
  check(iso.slice(0, 19) === v.slice(0, 19), "Invalid timestamp");
  return iso;
}
function magnitude(v: unknown, integer: boolean) {
  check(
    typeof v === "number" &&
      Number.isFinite(v) &&
      v >= 0 &&
      v <= 2147483647 &&
      (!integer || Number.isSafeInteger(v)),
    "Invalid measurement",
  );
  return v;
}
function optional(v: unknown, integer: boolean) {
  return v == null ? null : magnitude(v, integer);
}
export const digest = (text: string) =>
  createHash("sha256").update(text).digest("hex");

export function submission(value: unknown) {
  const v = fields(value, [
    "id",
    "participantId",
    "startedAt",
    "endedAt",
    "status",
    "durationSeconds",
    "gameVersion",
    "missions",
    "distractions",
    "quiz",
  ]);
  const id = uuid(v.id),
    participantId = uuid(v.participantId);
  const startedAt = instant(v.startedAt),
    endedAt = instant(v.endedAt);
  check(
    endedAt >= startedAt && Date.parse(endedAt) <= Date.now() + 300000,
    "Invalid session interval",
  );
  check(
    v.status === "completed" || v.status === "interrupted",
    "Invalid session status",
  );
  const status = v.status;
  check(
    typeof v.gameVersion === "string" &&
      v.gameVersion.trim().length > 0 &&
      v.gameVersion.length <= 80 &&
      !/[\x00-\x1f]/.test(v.gameVersion),
    "Invalid game version",
  );
  const durationSeconds = optional(v.durationSeconds, false);
  check(
    Array.isArray(v.missions) && v.missions.length === 4,
    "Expected four mission results",
  );
  const missions = v.missions
    .map((raw) => {
      const m = fields(raw, ["id", "completed", "itemsDone", "itemsTotal"]);
      const id = missionIds.find((k) => k === m.id);
      check(id && typeof m.completed === "boolean", "Invalid mission");
      const itemsDone = optional(m.itemsDone, true),
        itemsTotal = optional(m.itemsTotal, true);
      check(
        (itemsDone === null) === (itemsTotal === null) &&
          (itemsDone === null || itemsDone <= itemsTotal!),
        "Invalid item counts",
      );
      if (id !== "meeting" && itemsTotal !== null)
        check(
          m.completed === (itemsTotal > 0 && itemsDone === itemsTotal),
          "Inconsistent mission completion",
        );
      return { id, completed: m.completed, itemsDone, itemsTotal };
    })
    .sort((a, b) => missionIds.indexOf(a.id) - missionIds.indexOf(b.id));
  check(new Set(missions.map((m) => m.id)).size === 4, "Duplicate mission");
  check(
    status !== "completed" || missions.every((m) => m.completed),
    "Completed session requires all four missions",
  );
  check(
    Array.isArray(v.distractions) && v.distractions.length <= kinds.length,
    "Invalid distraction list",
  );
  const distractions = v.distractions
    .map((raw) => {
      const d = fields(raw, ["kind", "count", "seconds"]);
      const kind = kinds.find((k) => k === d.kind);
      check(kind, "Unknown distraction");
      const count = optional(d.count, true),
        seconds = optional(d.seconds, false);
      check(
        count !== null || seconds !== null,
        "Distraction row needs an observation",
      );
      return { kind, count, seconds };
    })
    .sort((a, b) => kinds.indexOf(a.kind) - kinds.indexOf(b.kind));
  check(
    new Set(distractions.map((d) => d.kind)).size === distractions.length,
    "Duplicate distraction",
  );
  let quiz: { total: number; answered: number; correct: number } | null = null;
  if (v.quiz != null) {
    const q = fields(v.quiz, ["total", "answered", "correct"]);
    const total = magnitude(q.total, true),
      answered = magnitude(q.answered, true),
      correct = magnitude(q.correct, true);
    check(
      total > 0 && correct <= answered && answered <= total,
      "Invalid quiz counts",
    );
    check(
      (answered === total) ===
        missions.find((m) => m.id === "meeting")!.completed,
      "Inconsistent meeting result",
    );
    quiz = { total, answered, correct };
  }
  return {
    id,
    participantId,
    startedAt,
    endedAt,
    status,
    durationSeconds,
    gameVersion: v.gameVersion,
    missions,
    distractions,
    quiz,
  };
}
export type Submission = ReturnType<typeof submission>;
