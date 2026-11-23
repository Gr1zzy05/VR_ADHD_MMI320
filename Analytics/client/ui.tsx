import { useContext, createContext } from "react";
import type { ReactNode, ComponentType } from "react";
import { ChevronRight, Inbox, ArrowUpRight } from "lucide-react";
import {
  AreaChart,
  Area,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  LineChart,
  Line,
} from "recharts";
import { count, date, duration, newest } from "./data";
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
export function ActivityChart({ rows }: { rows: Session[] }) {
  const { lang, t } = useLocale();
  const entries = newest(rows);
  if (!entries.length) return <Empty />;
  const lo = new Date(entries[entries.length - 1].startedAt),
    hi = new Date(entries[0].startedAt);
  const long = hi.valueOf() - lo.valueOf() > 90 * 86400000;
  const start = new Date(lo);
  start.setUTCHours(0, 0, 0, 0);
  if (long) start.setUTCDate(1);
  const key = (d: Date) => d.toISOString().slice(0, long ? 7 : 10);
  const buckets = new Map<string, number>();
  for (const s of rows) {
    const k = key(new Date(s.startedAt));
    buckets.set(k, (buckets.get(k) ?? 0) + 1);
  }
  const chart: { label: string; value: number }[] = [];
  for (
    const day = new Date(start);
    day <= hi;
    long
      ? day.setUTCMonth(day.getUTCMonth() + 1)
      : day.setUTCDate(day.getUTCDate() + 1)
  ) {
    chart.push({
      label: new Intl.DateTimeFormat(lang === "th" ? "th-TH" : "en-GB", {
        timeZone: "UTC",
        month: "short",
        ...(long ? { year: "2-digit" as const } : { day: "numeric" as const }),
      }).format(day),
      value: buckets.get(key(day)) ?? 0,
    });
  }
  const title = t("Sessions", "รอบกิจกรรม");
  return (
    <>
      <div className="chart-legend">
        <i />
        {title}
      </div>
      <div
        className="plot"
        role="img"
        aria-label={t("Session count over time", "จำนวนรอบกิจกรรมตามเวลา")}
      >
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart
            data={chart}
            margin={{ top: 10, right: 12, bottom: 2, left: -23 }}
          >
            <defs>
              <linearGradient id="volumeGradient" x1="0" x2="0" y1="0" y2="1">
                <stop offset="0%" stopColor="#5b85f3" stopOpacity=".32" />
                <stop offset="100%" stopColor="#5b85f3" stopOpacity=".015" />
              </linearGradient>
            </defs>
            <CartesianGrid
              stroke="#e2e8f0"
              strokeDasharray="3 4"
              vertical={false}
            />
            <XAxis
              dataKey="label"
              tickLine={false}
              axisLine={false}
              minTickGap={20}
              tickMargin={10}
            />
            <YAxis allowDecimals={false} tickLine={false} axisLine={false} />
            <Tooltip />
            <Area
              type="linear"
              dataKey="value"
              name={title}
              stroke="#386ae4"
              strokeWidth={2.7}
              fill="url(#volumeGradient)"
              dot={chart.length < 2}
              isAnimationActive={false}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </>
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
