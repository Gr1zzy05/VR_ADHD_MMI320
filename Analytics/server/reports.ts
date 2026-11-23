import { check, missionIds, uuid } from "./model.ts";
import type { Records, Session } from "./store.ts";

type Query = Record<string, unknown>;
function date(q: unknown) {
  if (q === undefined || q === "") return "";
  check(
    typeof q === "string" &&
      /^\d{4}-\d\d-\d\d$/.test(q) &&
      Number.isFinite(Date.parse(q)) &&
      new Date(q).toISOString().startsWith(q + "T"),
    "Invalid UTC date",
  );
  return q;
}
export function filtered(data: Records, query: Query) {
  const from = date(query.from),
    to = date(query.to);
  check(!from || !to || to >= from, "End date precedes start date");
  const participant = query.participant ? uuid(query.participant) : "";
  const mission = query.mission || "";
  check(
    !mission || missionIds.some((m) => m === mission),
    "Invalid mission filter",
  );
  const sessions = data.sessions.filter((s) => {
    const day = s.startedAt.slice(0, 10);
    return (
      (!from || day >= from) &&
      (!to || day <= to) &&
      (!participant || participant === s.participantId) &&
      (!mission || s.missions.some((m) => m.id === mission && m.completed))
    );
  });
  const included = new Set(sessions.map((s) => s.participantId));
  const participants = data.participants.filter(
    (p) =>
      (!participant || p.id === participant) &&
      (!(from || to || mission) || included.has(p.id)),
  );
  return { participants, sessions };
}
const mean = (values: number[]) =>
  values.length ? values.reduce((s, n) => s + n, 0) / values.length : null;
const numbers = (values: (number | null | undefined)[]) =>
  values.filter((v): v is number => typeof v === "number");
export function summary(data: ReturnType<typeof filtered>, mission?: unknown) {
  const { sessions } = data;
  const tasks = sessions
    .flatMap((s) => s.missions)
    .filter((m) => !mission || m.id === mission);
  const counts = numbers(
    sessions.flatMap((s) => s.distractions.map((d) => d.count)),
  );
  return {
    participants: data.participants.length,
    sessions: sessions.length,
    completed: sessions.filter((s) => s.status === "completed").length,
    averageDurationSeconds: mean(
      numbers(sessions.map((s) => s.durationSeconds)),
    ),
    taskCompletionPercentage: tasks.length
      ? (tasks.filter((m) => m.completed).length * 100) / tasks.length
      : null,
    averageQuizPercentage: mean(
      numbers(sessions.map((s) => s.quiz?.percentage)),
    ),
    recordedInteractions: counts.length
      ? counts.reduce((a, b) => a + b, 0)
      : null,
  };
}
type Cell = string | number | null | undefined;
function escapeCell(v: Cell) {
  if (v == null) return "";
  let s = String(v);
  // Keep a spreadsheet from interpreting arbitrary app version text as a formula.
  if (typeof v === "string" && /^[\s]*[=+@-]|^[\t\r\n]/.test(s)) s = "'" + s;
  return '"' + s.replaceAll('"', '""') + '"';
}
export function csv(
  data: ReturnType<typeof filtered>,
  type: unknown,
  mission: unknown,
) {
  const common = ["participant_code", "session_id", "started_at_utc", "status"];
  const codes = new Map(data.participants.map((p) => [p.id, p.code]));
  let header: string[], detail: (s: Session) => Cell[][];
  switch (type) {
    case "sessions":
      header = [
        "ended_at_utc",
        "game_version",
        "activity_seconds",
        "tasks_completed",
        "quiz_total",
        "quiz_answered",
        "quiz_correct",
        "quiz_incorrect",
        "quiz_percent",
        "recorded_interactions",
      ];
      detail = (s) => {
        const counts = numbers(s.distractions.map((d) => d.count));
        return [
          [
            s.endedAt,
            s.gameVersion,
            s.durationSeconds,
            s.missions.filter((m) => m.completed).length,
            s.quiz?.total,
            s.quiz?.answered,
            s.quiz?.correct,
            s.quiz?.incorrect,
            s.quiz?.percentage,
            counts.length ? counts.reduce((a, b) => a + b, 0) : null,
          ],
        ];
      };
      break;
    case "missions":
      header = [
        "mission",
        "completed",
        "items_done",
        "items_total",
        "time_seconds",
        "focus_seconds",
        "distraction_count",
      ];
      detail = (s) =>
        s.missions
          .filter((m) => !mission || m.id === mission)
          .map((m) => [
            m.id,
            Number(m.completed),
            m.itemsDone,
            m.itemsTotal,
            m.timeSeconds,
            m.focusSeconds,
            m.distractionCount,
          ]);
      break;
    case "distractions":
      header = [
        "distraction",
        "interaction_count",
        "interaction_seconds",
        "related_mission",
      ];
      detail = (s) =>
        s.distractions.map((d) => [d.kind, d.count, d.seconds, d.missionId]);
      break;
    case "quiz":
      header = ["questions", "answered", "correct", "incorrect", "percent"];
      detail = (s) =>
        s.quiz
          ? [
              [
                s.quiz.total,
                s.quiz.answered,
                s.quiz.correct,
                s.quiz.incorrect,
                s.quiz.percentage,
              ],
            ]
          : [];
      break;
    default:
      check(false, "Invalid export type");
  }
  const rows = [common.concat(header).join(",")];
  for (const s of data.sessions)
    for (const r of detail(s))
      rows.push(
        [codes.get(s.participantId), s.id, s.startedAt, s.status, ...r]
          .map(escapeCell)
          .join(","),
      );
  return "\uFEFF" + rows.join("\r\n") + "\r\n";
}
