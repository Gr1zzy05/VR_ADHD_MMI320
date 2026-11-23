export type Role = "admin" | "educator";
export type Lang = "en" | "th";
export type Translate = (english: string, thai: string) => string;
export type User = { role: Role; demo: boolean };
export type Participant = {
  id: string;
  code: string;
  nickname: string | null;
  registeredAt: string;
  consentedAt: string | null;
};
export const missionLabels = {
  washing: ["Wash dishes", "ล้างจาน"],
  organizing: ["Organize dishes", "จัดเก็บจาน"],
  trash: ["Take trash outside", "นำขยะไปทิ้ง"],
  meeting: ["Meeting + quiz", "ประชุมและตอบคำถาม"],
} as const;
export const distractionLabels = {
  FidgetCube: ["Fidget cube", "ลูกบาศก์ฟิดเจ็ต"],
  PopIt: ["Rainbow pop-it", "ป๊อปอิทสีรุ้ง"],
  Squishy: ["Butter squishy", "สกุชชี่เนย"],
  Television: ["Television", "โทรทัศน์"],
  Plushie: ["Plushie", "ตุ๊กตาผ้า"],
  Phone: ["Phone", "โทรศัพท์"],
  Conversation: ["Neighbour chatter", "เสียงเพื่อนบ้านคุยกัน"],
  Party: ["Neighbour party", "เสียงงานเลี้ยงข้างบ้าน"],
  Construction: ["Construction site", "เสียงก่อสร้าง"],
} as const;
export type MissionId = keyof typeof missionLabels;
export type DistractionKind = keyof typeof distractionLabels;
export type Mission = {
  id: MissionId;
  completed: boolean;
  itemsDone: number | null;
  itemsTotal: number | null;
  timeSeconds: number | null;
  focusSeconds: number | null;
  distractionCount: number | null;
};
export type Distraction = {
  kind: DistractionKind;
  count: number | null;
  seconds: number | null;
  missionId: MissionId | null;
};
export type Quiz = {
  total: number;
  answered: number;
  correct: number;
  incorrect: number;
  percentage: number | null;
};
export type Session = {
  id: string;
  participantId: string;
  startedAt: string;
  endedAt: string;
  status: "completed" | "interrupted";
  durationSeconds: number | null;
  gameVersion: string;
  missions: Mission[];
  distractions: Distraction[];
  quiz: Quiz | null;
};
export type RecordsData = { participants: Participant[]; sessions: Session[] };
export const missions = Object.keys(missionLabels) as MissionId[];
export const kinds = Object.keys(distractionLabels) as DistractionKind[];
export const finite = (n: number | null | undefined): n is number =>
  typeof n === "number" && Number.isFinite(n);
export function average(ns: (number | null | undefined)[]) {
  const v = ns.filter(finite);
  return v.length ? v.reduce((a, b) => a + b, 0) / v.length : null;
}
export function sum(ns: (number | null | undefined)[]) {
  const values = ns.filter(finite);
  return values.length ? values.reduce((a, b) => a + b, 0) : null;
}
export const newest = (rows: Session[]) =>
  [...rows].sort((a, b) => b.startedAt.localeCompare(a.startedAt));
export const memberSessions = (rows: Session[], id: string) =>
  newest(rows.filter((s) => s.participantId === id));
export function stats(rows: Session[]) {
  const completed = rows.filter((s) => s.status === "completed");
  const allMissions = rows.flatMap((s) => s.missions);
  return {
    completed: completed.length,
    duration: average(rows.map((s) => s.durationSeconds)),
    completionDuration: average(completed.map((s) => s.durationSeconds)),
    completion: average(allMissions.map((m) => (m.completed ? 100 : 0))),
    quiz: average(rows.map((s) => s.quiz?.percentage)),
    uses: sum(rows.flatMap((s) => s.distractions.map((d) => d.count))),
  };
}
export const pct = (v: number | null) =>
  v === null ? "—" : `${Number(v.toFixed(1))}%`;
export function duration(v: number | null) {
  if (v === null) return "—";
  const seconds = Math.round(v);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
}
export const nonce = "2026-01";
export function date(
  value: string | null | undefined,
  lang: Lang,
  time = false,
) {
  if (!value) return "—";
  return new Intl.DateTimeFormat(lang === "th" ? "th-TH" : "en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
    ...(time
      ? { hour: "2-digit", minute: "2-digit", hourCycle: "h23" as const }
      : {}),
  }).format(new Date(value));
}
export const count = (n: number | null) =>
  n === null ? "—" : n.toLocaleString();
export async function api<T>(
  url: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const res = await fetch(url, {
    method,
    credentials: "same-origin",
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) {
    let message = "";
    try {
      const detail = await res.json();
      message = typeof detail.error === "string" ? detail.error : "";
    } catch {}
    throw new Error(message || String(res.status));
  }
  if (res.status === 204 || res.headers.get("content-length") === "0")
    return undefined as T;
  return res.json() as Promise<T>;
}
export function download(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1500);
}
export function periodRows(rows: Session[], days: number) {
  if (!days) return rows;
  const today = new Date();
  today.setUTCHours(0, 0, 0, 0);
  const cutoff = today.valueOf() - (days - 1) * 86400000;
  return rows.filter((s) => Date.parse(s.startedAt) >= cutoff);
}
