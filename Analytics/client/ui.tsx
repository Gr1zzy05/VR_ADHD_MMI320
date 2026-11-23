import { useContext, createContext } from "react";
import type { ReactNode, ComponentType } from "react";
import { ChevronRight, Inbox, ArrowUpRight } from "lucide-react";
import {
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  LineChart,
  Line,
} from "recharts";
import { average, count, date, duration, finite, newest } from "./data";
import type { Lang, Participant, Session, Translate } from "./data";

export const Locale = createContext<{ lang: Lang; t: Translate }>({
  lang: "en",
  t: (a) => a,
});
export const useLocale = () => useContext(Locale);
export function Brand() {
  return (
    <div className="brand">
      <img
        src={new URL("./mark.svg", import.meta.url).href}
        alt=""
        width="38"
        height="38"
      />
      <span>Focus Shift</span>
    </div>
  );
}
export function Empty({ children }: { children?: ReactNode }) {
  const { t } = useLocale();
  return (
    <div className="empty">
      <Inbox size={26} />
      <span>
        {children ??
          t("No records in this selection", "ไม่มีข้อมูลในช่วงที่เลือก")}
      </span>
    </div>
  );
}
export function Header({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children?: ReactNode;
}) {
  return (
    <header className="pagehead">
      <div>
        <h1>{title}</h1>
        <p>{subtitle}</p>
      </div>
      {children}
    </header>
  );
}
type Icon = ComponentType<{ size?: number; strokeWidth?: number }>;
export function Metric({
  icon: Glyph,
  label,
  value,
  detail,
  tone = "blue",
}: {
  icon: Icon;
  label: string;
  value: string | number;
  detail: string;
  tone?: string;
}) {
  return (
    <article className="metric">
      <div className="metric-head">
        <span>{label}</span>
        <span className={"icon-chip " + tone}>
          <Glyph size={18} strokeWidth={1.8} />
        </span>
      </div>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}
export function Card({
  title,
  subtitle,
  action,
  children,
  className = "",
}: {
  title: string;
  subtitle?: string;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={"card " + className}>
      <header className="cardhead">
        <div>
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
        {action}
      </header>
      {children}
    </section>
  );
}
export function Status({ complete }: { complete: boolean }) {
  const { t } = useLocale();
  return (
    <span className={"badge " + (complete ? "complete" : "interrupt")}>
      <i />
      {complete ? t("Completed", "เสร็จสิ้น") : t("Interrupted", "หยุดกลางคัน")}
    </span>
  );
}
export function TextLink({
  to,
  children,
}: {
  to: string;
  children: ReactNode;
}) {
  return (
    <a className="textlink" href={"#" + to}>
      {children}
      <ChevronRight size={16} />
    </a>
  );
}
export function SessionTable({
  rows,
  people,
  showParticipant = true,
}: {
  rows: Session[];
  people: Participant[];
  showParticipant?: boolean;
}) {
  const { lang, t } = useLocale();
  const map = new Map(people.map((p) => [p.id, p]));
  if (!rows.length) return <Empty />;
  return (
    <div className="tablewrap">
      <table>
        <thead>
          <tr>
            {showParticipant && <th>{t("Participant", "ผู้เข้าร่วม")}</th>}
            <th>{t("Started (UTC)", "เริ่มต้น (UTC)")}</th>
            <th>{t("Status", "สถานะ")}</th>
            <th>{t("Duration", "ระยะเวลา")}</th>
            <th>{t("Tasks", "ภารกิจ")}</th>
            <th>{t("Correct / answered", "ตอบถูก / ตอบแล้ว")}</th>
            <th aria-label={t("Report", "รายงาน")} />
          </tr>
        </thead>
        <tbody>
          {newest(rows).map((s) => (
            <tr key={s.id}>
              {showParticipant && (
                <td>
                  <a
                    href={"#/participants/" + s.participantId}
                    className="code"
                  >
                    <span className="avatar-sm">
                      {(map.get(s.participantId)?.code ?? "P").slice(0, 1)}
                    </span>
                    {map.get(s.participantId)?.code ?? "—"}
                  </a>
                </td>
              )}
              <td>
                <a href={"#/sessions/" + s.id}>
                  {date(s.startedAt, lang, true)}
                </a>
              </td>
              <td>
                <Status complete={s.status === "completed"} />
              </td>
              <td className="num">{duration(s.durationSeconds)}</td>
              <td className="num">
                {s.missions.filter((m) => m.completed).length}/
                {s.missions.length}
              </td>
              <td className="num">
                {s.quiz ? s.quiz.correct + "/" + s.quiz.answered : "—"}
              </td>
              <td>
                <a
                  className="row-open"
                  aria-label={
                    t("Open session report", "เปิดรายงานรอบกิจกรรม") +
                    " " +
                    s.id
                  }
                  href={"#/sessions/" + s.id}
                >
                  <ArrowUpRight size={16} />
                </a>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
export function ParticipantDurationChart({ rows, participants }: {
  rows: Session[];
  participants: Participant[];
}) {
  const { t } = useLocale();
  const byParticipant = new Map<string, (number | null)[]>();
  for (const row of rows) {
    const times = byParticipant.get(row.participantId) ?? [];
    times.push(row.durationSeconds);
    byParticipant.set(row.participantId, times);
  }
  const chart = participants
    .flatMap(({ id, code }) => {
      const times = byParticipant.get(id);
      return times ? [{ code, seconds: average(times), measured: times.filter(finite).length, rounds: times.length }] : [];
    })
    .sort((a, b) => a.code.localeCompare(b.code, undefined, { numeric: true }));
  const title = t("Average play time", "เวลาเล่นเฉลี่ย");
  if (!chart.some(({ seconds }) => seconds !== null)) {
    return <Empty>{t("No play times recorded in this period", "ไม่มีเวลาเล่นที่บันทึกในช่วงที่เลือก")}</Empty>;
  }
  return (
    <div style={{ overflowX: "auto" }}>
      <div className="plot" style={{ minWidth: Math.max(0, chart.length * 62 + 54) }}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={chart} margin={{ top: 12, right: 16, bottom: 6, left: 0 }}>
            <CartesianGrid stroke="#e2e8f0" strokeDasharray="3 4" vertical={false} />
            <XAxis dataKey="code" tickLine={false} axisLine={false} tickMargin={10} interval={0}
              padding={{ left: 24, right: 24 }} />
            <YAxis
              tickFormatter={(value: number) => duration(value)}
              tickLine={false}
              axisLine={false}
              domain={[0, (max: number) => Math.max(1, max)]}
              allowDecimals={false}
              width={52}
            />
            <Tooltip
              filterNull={false}
              formatter={(_value, _name, item) => {
                const row = item.payload as (typeof chart)[number];
                return [
                  <>
                    {duration(row.seconds)}
                    {" · "}{t("Timed rounds", "รอบที่มีเวลา")}{` ${row.measured}/${row.rounds}`}
                  </>,
                  title,
                ];
              }}
            />
            <Line type="linear" dataKey="seconds" name={title} stroke="#386ae4" strokeWidth={2.5}
              dot={{ r: 4 }} connectNulls={false} isAnimationActive={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

export function RepeatChart({
  rows,
  measure,
}: {
  rows: Session[];
  measure: "minutes" | "quiz" | "uses";
}) {
  const { t } = useLocale();
  const ordered = newest(rows).reverse();
  const label =
    measure === "minutes"
      ? t("Activity duration (min)", "ระยะเวลากิจกรรม (นาที)")
      : measure === "uses"
        ? t("Recorded interactions", "การโต้ตอบที่บันทึก")
        : t("Quiz correctness (%)", "ตอบถูก (%)");
  const data = ordered.map((s, i) => ({
    n: i + 1,
    v:
      measure === "quiz"
        ? (s.quiz?.percentage ?? null)
        : measure === "minutes"
          ? s.durationSeconds === null
            ? null
            : Number((s.durationSeconds / 60).toFixed(2))
          : s.distractions.some((d) => d.count !== null)
            ? s.distractions.reduce((a, d) => a + (d.count ?? 0), 0)
            : null,
  }));
  if (!data.some((d) => d.v !== null)) return <Empty />;
  return (
    <div className="plot" role="img" aria-label={label}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart
          data={data}
          margin={{ top: 12, right: 18, left: -15, bottom: 16 }}
        >
          <CartesianGrid
            stroke="#e2e8f0"
            strokeDasharray="3 4"
            vertical={false}
          />
          <XAxis
            dataKey="n"
            tickLine={false}
            axisLine={false}
            label={{
              value: t("Session number", "ลำดับรอบกิจกรรม"),
              position: "insideBottom",
              offset: -12,
            }}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            domain={measure === "quiz" ? [0, 100] : [0, "auto"]}
          />
          <Tooltip
            labelFormatter={(n) => t("Session", "รอบกิจกรรม") + " " + n}
          />
          <Line
            dataKey="v"
            name={label}
            type="linear"
            stroke="#386ae4"
            strokeWidth={3}
            dot={{ r: 4, fill: "#fff" }}
            connectNulls={false}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
export function SummaryLine({
  label,
  value,
}: {
  label: string;
  value: number | string | null;
}) {
  return (
    <div className="summary-line">
      <span>{label}</span>
      <b>
        {typeof value === "number" || value === null ? count(value) : value}
      </b>
    </div>
  );
}
