import { useState } from "react";
import {
  CalendarDays,
  Users,
  Layers3,
  CircleCheck,
  Clock3,
  ClipboardList,
  Target,
  MousePointerClick,
  Search,
  QrCode,
} from "lucide-react";
import {
  ParticipantDurationChart,
  Card,
  Empty,
  Header,
  Metric,
  RepeatChart,
  SessionTable,
  Status,
  SummaryLine,
  TextLink,
  useLocale,
} from "./ui";
import {
  average,
  count,
  date,
  distractionLabels,
  duration,
  finite,
  kinds,
  memberSessions,
  missionLabels,
  missions,
  newest,
  pct,
  periodRows,
  stats,
  sum,
} from "./data";
import type { Participant, RecordsData, Session } from "./data";
import {
  ParticipantActions,
  Registration,
  Reports,
  ResetPlayerCodes,
} from "./management";
import { ResultShare } from "./result-share";
export { Reports };

function Period({ value, set }: { value: number; set: (v: number) => void }) {
  const { t } = useLocale();
  return (
    <label className="select-wrap period">
      <CalendarDays size={16} />
      <select
        aria-label={t("Activity period", "ช่วงเวลากิจกรรม")}
        value={value}
        onChange={(e) => set(Number(e.target.value))}
      >
        <option value="0">{t("All time", "ทุกช่วงเวลา")}</option>
        <option value="30">{t("Last 30 days", "30 วันที่ผ่านมา")}</option>
        <option value="90">{t("Last 90 days", "90 วันที่ผ่านมา")}</option>
      </select>
    </label>
  );
}
function MissionBars({ rows }: { rows: Session[] }) {
  const { t } = useLocale();
  return (
    <div className="missionbars">
      {missions.map((id, i) => {
        const results = rows.flatMap((s) =>
          s.missions.filter((m) => m.id === id),
        );
        const rate = average(results.map((m) => (m.completed ? 100 : 0)));
        return (
          <div className="missionbar" key={id}>
            <div>
              <span>{t(missionLabels[id][0], missionLabels[id][1])}</span>
              <b>{pct(rate)}</b>
            </div>
            <div className="bar-track">
              <div
                style={{
                  width: (rate ?? 0) + "%",
                  background: ["#426fe2", "#6488e8", "#65a3c7", "#38a29d"][i],
                }}
              />
            </div>
          </div>
        );
      })}
    </div>
  );
}
export function Overview({ data }: { data: RecordsData }) {
  const { t } = useLocale();
  const [days, period] = useState(0);
  const rows = periodRows(data.sessions, days),
    metrics = stats(rows);
  const people = days
    ? new Set(rows.map((s) => s.participantId)).size
    : data.participants.length;
  return (
    <>
      <Header
        title={t("Overview dashboard", "ภาพรวมกิจกรรม")}
        subtitle={t(
          "Participation and activity results from the Focus Shift VR simulation.",
          "การเข้าร่วมและผลกิจกรรมจากการจำลอง VR ของ Focus Shift",
        )}
      >
        <Period value={days} set={period} />
      </Header>
      <div className="metric-grid">
        <Metric
          icon={Users}
          label={
            days
              ? t("Active participants", "ผู้เข้าร่วมในช่วงเวลา")
              : t("Total participants", "ผู้เข้าร่วมทั้งหมด")
          }
          value={count(people)}
          detail={
            days
              ? t("With activity in this period", "มีกิจกรรมในช่วงเวลานี้")
              : t("Registered participant codes", "รหัสผู้เข้าร่วมที่ลงทะเบียน")
          }
        />
        <Metric
          icon={Layers3}
          label={t("Total sessions", "รอบกิจกรรมทั้งหมด")}
          value={count(rows.length)}
          detail={t(
            "Completed and interrupted",
            "รวมรอบที่เสร็จสิ้นและหยุดกลางคัน",
          )}
          tone="violet"
        />
        <Metric
          icon={CircleCheck}
          label={t("Completed sessions", "รอบที่เสร็จสิ้น")}
          value={count(metrics.completed)}
          detail={t(
            "All four activities completed",
            "ทำกิจกรรมครบทั้งสี่ภารกิจ",
          )}
          tone="teal"
        />
        <Metric
          icon={Clock3}
          label={t("Average duration", "ระยะเวลาเฉลี่ย")}
          value={duration(metrics.duration)}
          detail={t(
            "Minutes : seconds per session",
            "นาที : วินาที ต่อรอบกิจกรรม",
          )}
          tone="amber"
        />
      </div>
      <div className="chart-grid">
        <Card
          title={t("Average play time by participant", "เวลาเล่นเฉลี่ยรายผู้เข้าร่วม")}
          subtitle={t(
            "Minutes : seconds · Includes rounds with recorded play time",
            "นาที : วินาที · คิดจากรอบที่มีเวลาเล่นบันทึกไว้",
          )}
        >
          <ParticipantDurationChart rows={rows} participants={data.participants} />
        </Card>
        <Card
          title={t("Mission completion", "การทำภารกิจสำเร็จ")}
          subtitle={t(
            "Share of recorded results completed",
            "สัดส่วนภารกิจที่ทำสำเร็จจากผลที่บันทึก",
          )}
        >
          <MissionBars rows={rows} />
        </Card>
      </div>
      <div className="strip">
        {[
          [
            ClipboardList,
            t("Overall task completion", "ทำภารกิจสำเร็จโดยรวม"),
            pct(metrics.completion),
          ],
          [
            Target,
            t("Average quiz correctness", "ตอบแบบทดสอบถูกเฉลี่ย"),
            pct(metrics.quiz),
          ],
          [
            MousePointerClick,
            t("Distraction interactions", "การโต้ตอบกับสิ่งรบกวน"),
            count(metrics.uses),
          ],
        ].map(([I, label, val]) => {
          const Icon = I as typeof Target;
          return (
            <div key={String(label)}>
              <span className="strip-icon">
                <Icon size={19} />
              </span>
              <div>
                <small>{String(label)}</small>
                <strong>{String(val)}</strong>
              </div>
            </div>
          );
        })}
      </div>
      <Card
        title={t("Recent sessions", "รอบกิจกรรมล่าสุด")}
        subtitle={t(
          "Select a session to review its activity report.",
          "เลือกรอบกิจกรรมเพื่ออ่านรายงานแต่ละรอบ",
        )}
        action={
          <TextLink to="/sessions">
            {t("View sessions", "ดูรอบกิจกรรม")}
          </TextLink>
        }
      >
        <SessionTable
          rows={newest(rows).slice(0, 5)}
          people={data.participants}
        />
      </Card>
    </>
  );
}
export function Sessions({ data }: { data: RecordsData }) {
  const { t } = useLocale();
  const [state, set] = useState("");
  const [days, period] = useState(0);
  return (
    <>
      <Header
        title={t("Session records", "บันทึกรอบกิจกรรม")}
        subtitle={t(
          "Review completed and interrupted sessions in chronological order.",
          "ดูรอบที่เสร็จสิ้นและหยุดกลางคันตามเวลา",
        )}
      >
        <Period value={days} set={period} />
      </Header>
      <Card
        title={t("Session history", "ประวัติรอบกิจกรรม")}
        action={
          <select
            aria-label={t("Status", "สถานะ")}
            value={state}
            onChange={(e) => set(e.target.value)}
          >
            <option value="">{t("All statuses", "ทุกสถานะ")}</option>
            <option value="completed">{t("Completed", "เสร็จสิ้น")}</option>
            <option value="interrupted">
              {t("Interrupted", "หยุดกลางคัน")}
            </option>
          </select>
        }
      >
        <SessionTable
          rows={periodRows(data.sessions, days).filter(
            (s) => !state || s.status === state,
          )}
          people={data.participants}
        />
      </Card>
    </>
  );
}
export function Participants({
  data,
  admin,
  refresh,
}: {
  data: RecordsData;
  admin: boolean;
  refresh: () => Promise<void>;
}) {
  const { lang, t } = useLocale();
  const [q, search] = useState(""),
    [order, sort] = useState("code"),
    [filter, setFilter] = useState("all"),
    [adding, setAdd] = useState(false);
  const [sharing, setSharing] = useState<string | null>(null);
  const selected = data.participants.find((p) => p.id === sharing);
  const entries = data.participants
    .map((p) => {
      const rows = memberSessions(data.sessions, p.id);
      return { p, rows, metrics: stats(rows) };
    })
    .filter(
      ({ p, rows }) =>
        (p.code + " " + (p.nickname ?? ""))
          .toLowerCase()
          .includes(q.trim().toLowerCase()) &&
        (filter === "all" ||
          (filter === "repeat" ? rows.length > 1 : rows.length === 0)),
    );
  entries.sort((a, b) =>
    order === "sessions"
      ? b.rows.length - a.rows.length
      : order === "latest"
        ? (b.rows[0]?.startedAt ?? "").localeCompare(a.rows[0]?.startedAt ?? "")
        : a.p.code.localeCompare(b.p.code, undefined, { numeric: true }),
  );
  return (
    <>
      <Header
        title={t("Participant records", "ทะเบียนผู้เข้าร่วม")}
        subtitle={t(
          "Find anonymous participant codes and review their session history.",
          "ค้นหารหัสผู้เข้าร่วมแบบไม่ระบุตัวตนและดูประวัติกิจกรรม",
        )}
      >
        {admin && (
          <div className="actions">
            <ResetPlayerCodes empty={data.participants.length === 0} />
            <button className="primary" onClick={() => setAdd(true)}>
              <Users size={16} />
              {t("Register participant", "ลงทะเบียนผู้เข้าร่วม")}
            </button>
          </div>
        )}
      </Header>
      {adding && <Registration close={() => setAdd(false)} refresh={refresh} />}
      {admin && selected?.consentedAt && (
        <ResultShare
          participant={selected}
          sessions={memberSessions(data.sessions, selected.id)}
          close={() => setSharing(null)}
        />
      )}
      <Card
        title={t("Participant directory", "รายชื่อรหัสผู้เข้าร่วม")}
        subtitle={t(
          "Average completion duration uses completed sessions.",
          "ระยะเวลาเฉลี่ยคิดจากรอบที่ทำเสร็จสิ้น",
        )}
      >
        <div className="filters">
          <label className="input-icon">
            <Search size={17} />
            <input
              aria-label={t("Search participant", "ค้นหาผู้เข้าร่วม")}
              placeholder={t(
                "Search code or nickname",
                "ค้นหารหัสหรือชื่อเล่น",
              )}
              value={q}
              onChange={(e) => search(e.target.value)}
            />
          </label>
          <select
            aria-label={t("Filter by participation", "กรองตามการเข้าร่วม")}
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          >
            <option value="all">
              {t("All participants", "ผู้เข้าร่วมทั้งหมด")}
            </option>
            <option value="repeat">
              {t("Repeat participants", "เข้าร่วมหลายรอบ")}
            </option>
            <option value="none">
              {t("No sessions yet", "ยังไม่มีรอบกิจกรรม")}
            </option>
          </select>
          <select
            aria-label={t("Sort participants", "เรียงผู้เข้าร่วม")}
            value={order}
            onChange={(e) => sort(e.target.value)}
          >
            <option value="code">{t("Sort by code", "เรียงตามรหัส")}</option>
            <option value="sessions">
              {t("Most sessions", "จำนวนรอบมากที่สุด")}
            </option>
            <option value="latest">
              {t("Latest activity", "กิจกรรมล่าสุด")}
            </option>
          </select>
        </div>
        {entries.length ? (
          <div className="tablewrap">
            <table>
              <thead>
                <tr>
                  {[
                    t("Code", "รหัส"),
                    t("Nickname", "ชื่อเล่น"),
                    t("Sessions", "จำนวนรอบ"),
                    t("Latest activity (UTC)", "กิจกรรมล่าสุด (UTC)"),
                    t("Avg. completion", "เวลาสำเร็จเฉลี่ย"),
                    t("Avg. quiz", "คะแนนเฉลี่ย"),
                  ].map((l) => (
                    <th key={l}>{l}</th>
                  ))}
                  {admin && <th>{t("Share results", "แชร์ผล")}</th>}
                </tr>
              </thead>
              <tbody>
                {entries.map(({ p, rows, metrics }) => (
                  <tr key={p.id}>
                    <td>
                      <a className="code" href={"#/participants/" + p.id}>
                        <span className="avatar-sm">P</span>
                        {p.code}
                      </a>
                    </td>
                    <td>{p.nickname ?? "—"}</td>
                    <td>{rows.length}</td>
                    <td>{date(rows[0]?.endedAt, lang)}</td>
                    <td>{duration(metrics.completionDuration)}</td>
                    <td>{pct(metrics.quiz)}</td>
                    {admin && (
                      <td>
                        <button
                          className="secondary"
                          disabled={!p.consentedAt || rows.length === 0}
                          aria-label={t("Create QR for " + p.code, "สร้าง QR สำหรับ " + p.code)}
                          title={!p.consentedAt || rows.length === 0
                            ? t("A recorded session and active consent are required", "ต้องมีรอบที่บันทึกแล้วและความยินยอมที่ยังมีผล")
                            : undefined}
                          onClick={() => setSharing(p.id)}
                        >
                          <QrCode size={16} /> {t("Create QR", "สร้าง QR")}
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <Empty />
        )}
      </Card>
    </>
  );
}
function Distractions({ rows, byFrequency = false }: { rows: Session[]; byFrequency?: boolean }) {
  const { t } = useLocale();
  const all = rows.flatMap((s) => s.distractions);
  const types = kinds.map(kind => {
    const results = all.filter(d => d.kind === kind);
    const paired = results.filter(d => finite(d.count) && finite(d.seconds));
    const pairedUses = sum(paired.map(d => d.count));
    return {
      kind,
      uses: sum(results.map(d => d.count)),
      seconds: sum(results.map(d => d.seconds)),
      meanSeconds: pairedUses ? paired.reduce((n, d) => n + d.seconds!, 0) / pairedUses : null,
    };
  });
  if (byFrequency) types.sort((a, b) => (b.uses ?? -1) - (a.uses ?? -1));
  const max = Math.max(0, ...types.map(row => row.uses ?? 0));
  return (
    <div className="tablewrap">
      <table>
        <thead>
          <tr>
            <th>{t("Distraction", "สิ่งรบกวน")}</th>
            <th>{t("Interactions", "จำนวนครั้ง")}</th>
            <th>{t("Recorded time (s)", "เวลาที่บันทึก (วินาที)")}</th>
            <th>{t("Seconds per interaction", "วินาทีต่อการโต้ตอบ")}</th>
          </tr>
        </thead>
        <tbody>
          {types.map(({kind, uses, seconds, meanSeconds}) => (
            <tr key={kind}>
              <td>{t(distractionLabels[kind][0], distractionLabels[kind][1])}</td>
              <td><span className="frequency-count">{count(uses)}{byFrequency && max > 0 && uses !== null && <span aria-hidden="true"><i style={{width: (100 * uses / max) + "%"}} /></span>}</span></td>
              <td>{seconds === null ? "—" : Number(seconds.toFixed(2))}</td>
              <td>{meanSeconds === null ? "—" : Number(meanSeconds.toFixed(2))}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
export function ParticipantDetail({
  data,
  p,
  admin,
  refresh,
}: {
  data: RecordsData;
  p: Participant;
  admin: boolean;
  refresh: () => Promise<void>;
}) {
  const { t, lang } = useLocale();
  const rows = memberSessions(data.sessions, p.id),
    m = stats(rows);
  const [measure, set] = useState<"minutes" | "uses" | "quiz">("minutes");
  return (
    <>
      <Header
        title={p.code + (p.nickname ? " · " + p.nickname : "")}
        subtitle={
          t("Registered", "ลงทะเบียน") +
          " " +
          date(p.registeredAt, lang) +
          " · " +
          t("Participant record", "ประวัติผู้เข้าร่วม")
        }
      />
      <div className="metric-grid">
        <Metric
          icon={Layers3}
          label={t("Total sessions", "รอบทั้งหมด")}
          value={rows.length}
          detail={t("Recorded participation", "การเข้าร่วมที่บันทึกไว้")}
        />
        <Metric
          icon={CircleCheck}
          label={t("Completed sessions", "รอบที่เสร็จสิ้น")}
          value={m.completed}
          detail={t("All four activities", "ทำครบสี่ภารกิจ")}
          tone="teal"
        />
        <Metric
          icon={Clock3}
          label={t("Avg. completion duration", "เวลาเฉลี่ยเมื่อสำเร็จ")}
          value={duration(m.completionDuration)}
          detail={t(
            "Completed sessions · min:s",
            "รอบที่เสร็จสิ้น · นาที:วินาที",
          )}
          tone="violet"
        />
        <Metric
          icon={Target}
          label={t("Avg. quiz correctness", "คะแนนเฉลี่ย")}
          value={pct(m.quiz)}
          detail={t("Finalized quizzes", "แบบทดสอบที่ทำครบ")}
          tone="amber"
        />
      </div>
      <div className="chart-grid">
        <Card
          title={t("Changes across sessions", "การเปลี่ยนแปลงระหว่างรอบ")}
          subtitle={t(
            "Recorded measurements in participation order.",
            "ค่าที่บันทึกเรียงตามลำดับการเข้าร่วม",
          )}
          action={
            <select
              aria-label={t("Measurement", "ตัวแปร")}
              value={measure}
              onChange={(e) => set(e.target.value as typeof measure)}
            >
              <option value="minutes">
                {t("Duration (min)", "เวลา (นาที)")}
              </option>
              <option value="uses">{t("Interactions", "การโต้ตอบ")}</option>
              <option value="quiz">{t("Quiz (%)", "คะแนน (%)")}</option>
            </select>
          }
        >
          <RepeatChart rows={rows} measure={measure} />
        </Card>
        <Card title={t("Participation details", "รายละเอียดการเข้าร่วม")}>
          <div className="padded">
            <SummaryLine
              label={t("First participation", "เข้าร่วมครั้งแรก")}
              value={date(rows.at(-1)?.startedAt, lang)}
            />
            <SummaryLine
              label={t("Latest participation", "เข้าร่วมล่าสุด")}
              value={date(rows[0]?.endedAt, lang)}
            />
            <SummaryLine
              label={t("Completed tasks", "ภารกิจที่สำเร็จ")}
              value={
                rows.flatMap((s) => s.missions).filter((m) => m.completed)
                  .length
              }
            />
            <SummaryLine
              label={t("Recorded interactions", "การโต้ตอบที่บันทึก")}
              value={m.uses}
            />
            <SummaryLine
              label={t("Consent", "ความยินยอม")}
              value={
                p.consentedAt ? t("Active", "มีผล") : t("Inactive", "ไม่มีผล")
              }
            />
          </div>
        </Card>
      </div>
      <Card title={t("Session history", "ประวัติรอบกิจกรรม")}>
        <SessionTable rows={rows} people={[p]} showParticipant={false} />
      </Card>
      <Card title={t("Distraction interactions", "การโต้ตอบกับสิ่งรบกวน")}>
        <Distractions rows={rows} />
      </Card>
      {admin && <ParticipantActions p={p} refresh={refresh} />}
    </>
  );
}
export function SessionDetail({
  s,
  p,
}: {
  s: Session;
  p: Participant | undefined;
}) {
  const { t, lang } = useLocale();
  return (
    <>
      <Header
        title={t("Session report", "รายงานรอบกิจกรรม")}
        subtitle={
          t("Activity record", "บันทึกกิจกรรม") +
          " · " +
          (p?.code ?? "—") +
          " · " +
          date(s.startedAt, lang, true) +
          " UTC"
        }
      >
        <Status complete={s.status === "completed"} />
      </Header>
      <Card
        title={t("Session information", "ข้อมูลรอบกิจกรรม")}
        action={p && <TextLink to={"/participants/" + p.id}>{p.code}</TextLink>}
      >
        <dl className="facts">
          {[
            [t("Session ID", "รหัสรอบกิจกรรม"), s.id],
            [t("Game version", "เวอร์ชันเกม"), s.gameVersion],
            [
              t("Started (UTC)", "เริ่มต้น (UTC)"),
              date(s.startedAt, lang, true),
            ],
            [t("Ended (UTC)", "สิ้นสุด (UTC)"), date(s.endedAt, lang, true)],
            [
              t("Activity duration (min:s)", "ระยะเวลา (นาที:วินาที)"),
              duration(s.durationSeconds),
            ],
            [
              t("Completed tasks", "ภารกิจที่สำเร็จ"),
              s.missions.filter((m) => m.completed).length +
                " / " +
                s.missions.length,
            ],
          ].map(([l, v]) => (
            <div key={l}>
              <dt>{l}</dt>
              <dd>{v}</dd>
            </div>
          ))}
        </dl>
      </Card>
      <Card
        title={t("Mission results", "ผลแต่ละภารกิจ")}
        subtitle={t(
          "A dash indicates an unavailable measurement.",
          "ขีดหมายถึงไม่มีค่าที่บันทึก",
        )}
      >
        <div className="tablewrap">
          <table>
            <thead>
              <tr>
                {[
                  t("Mission", "ภารกิจ"),
                  t("Status", "สถานะ"),
                  t("Item progress", "จำนวนชิ้น"),
                  t("Time (min:s)", "เวลา (นาที:วินาที)"),
                  t("Focus (min:s)", "เวลาจดจ่อ (นาที:วินาที)"),
                  t("Distractions", "การโต้ตอบ"),
                ].map((l) => (
                  <th key={l}>{l}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {missions.map((id) => {
                const m = s.missions.find((x) => x.id === id);
                return (
                  <tr key={id}>
                    <td>{t(missionLabels[id][0], missionLabels[id][1])}</td>
                    <td>
                      {m
                        ? m.completed
                          ? t("Completed", "เสร็จสิ้น")
                          : t("Incomplete", "ยังไม่เสร็จ")
                        : "—"}
                    </td>
                    <td>
                      {m && m.itemsDone !== null && m.itemsTotal !== null
                        ? m.itemsDone + "/" + m.itemsTotal
                        : "—"}
                    </td>
                    <td>{duration(m?.timeSeconds ?? null)}</td>
                    <td>{duration(m?.focusSeconds ?? null)}</td>
                    <td>{count(m?.distractionCount ?? null)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </Card>
      <Card
        title={t("Meeting quiz", "แบบทดสอบการประชุม")}
        subtitle={t(
          "Unanswered questions are excluded from incorrect answers.",
          "ข้อที่ยังไม่ตอบจะไม่นับเป็นข้อที่ตอบผิด",
        )}
      >
        {s.quiz ? (
          <dl className="facts">
            {[
              [t("Total questions", "จำนวนคำถาม"), s.quiz.total],
              [t("Answered", "ตอบแล้ว"), s.quiz.answered],
              [t("Correct", "ตอบถูก"), s.quiz.correct],
              [t("Incorrect", "ตอบผิด"), s.quiz.incorrect],
              [t("Final score", "คะแนนเมื่อทำครบ"), pct(s.quiz.percentage)],
            ].map(([l, v]) => (
              <div key={l}>
                <dt>{l}</dt>
                <dd>{v}</dd>
              </div>
            ))}
          </dl>
        ) : (
          <Empty>
            {t("No quiz attempt recorded", "ไม่มีการบันทึกการทำแบบทดสอบ")}
          </Empty>
        )}
      </Card>
      <Card title={t("Distraction interactions", "การโต้ตอบกับสิ่งรบกวน")}>
        <Distractions rows={[s]} />
      </Card>
    </>
  );
}
export function Behavior({ data }: { data: RecordsData }) {
  const { t } = useLocale();
  const [days, period] = useState(0);
  const rows = periodRows(data.sessions, days),
    m = stats(rows);
  const byParticipant = data.participants.map((p) =>
    memberSessions(data.sessions, p.id).reverse(),
  );
  const rounds = new Map<number, Session[]>();
  const ids = new Set(rows.map((s) => s.id));
  byParticipant.forEach((sequence) =>
    sequence.forEach((s, i) => {
      if (sequence.length < 2 || !ids.has(s.id)) return;
      const group = rounds.get(i + 1) ?? [];
      group.push(s);
      rounds.set(i + 1, group);
    }),
  );
  return (
    <>
      <Header
        title={t("Behavior analytics", "วิเคราะห์พฤติกรรม")}
        subtitle={t(
          "Explore observed activity, distraction interactions and quiz responses.",
          "สำรวจกิจกรรมที่สังเกตได้ การโต้ตอบกับสิ่งรบกวน และผลการตอบคำถาม",
        )}
      >
        <Period value={days} set={period} />
      </Header>
      <div className="metric-grid three">
        <Metric
          icon={Clock3}
          label={t("Avg. completed session", "เวลาเฉลี่ยรอบที่เสร็จสิ้น")}
          value={duration(m.completionDuration)}
          detail={t("Minutes : seconds", "นาที : วินาที")}
        />
        <Metric
          icon={MousePointerClick}
          label={t("Recorded interactions", "การโต้ตอบที่บันทึก")}
          value={count(m.uses)}
          detail={t("Across distraction types", "รวมสิ่งรบกวนทุกประเภท")}
          tone="teal"
        />
        <Metric
          icon={Target}
          label={t("Avg. quiz correctness", "ตอบถูกเฉลี่ย")}
          value={pct(m.quiz)}
          detail={t("Completed quiz attempts", "เฉพาะแบบทดสอบที่ทำครบ")}
          tone="violet"
        />
      </div>
      <Card
        title={t(
          "Distraction frequency and duration",
          "ความถี่และระยะเวลาการโต้ตอบ",
        )}
        subtitle={t(
          "Counts reflect the engagement rule for each object or environmental sound.",
          "จำนวนครั้งขึ้นอยู่กับเงื่อนไขการโต้ตอบของวัตถุหรือเสียงแต่ละชนิด",
        )}
      >
        <Distractions rows={rows} byFrequency />
      </Card>
      <div className="chart-grid">
        <Card
          title={t("Repeated participation", "การเข้าร่วมหลายรอบ")}
          subtitle={t(
            "Averages by session order among returning participants.",
            "ค่าเฉลี่ยตามลำดับรอบของผู้ที่เข้าร่วมมากกว่าหนึ่งครั้ง",
          )}
        >
          {rounds.size ? (
            <div className="tablewrap">
              <table>
                <thead>
                  <tr>
                    {[
                      t("Run", "รอบที่"),
                      t("Sessions", "จำนวนรอบ"),
                      t("Avg. duration", "ระยะเวลาเฉลี่ย"),
                      t("Avg. quiz", "คะแนนเฉลี่ย"),
                    ].map((l) => (
                      <th key={l}>{l}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {[...rounds]
                    .sort(([a], [b]) => a - b)
                    .map(([n, group]) => {
                      const g = stats(group);
                      return (
                        <tr key={n}>
                          <td>{n}</td>
                          <td>{group.length}</td>
                          <td>{duration(g.duration)}</td>
                          <td>{pct(g.quiz)}</td>
                        </tr>
                      );
                    })}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty />
          )}
        </Card>
        <Card
          title={t("Average mission duration", "เวลาเฉลี่ยรายภารกิจ")}
          subtitle={t(
            "Uses recorded per-mission timers when available.",
            "คำนวณจากตัวจับเวลารายภารกิจเมื่อมีการบันทึก",
          )}
        >
          <div className="padded">
            {missions.map((id) => (
              <SummaryLine
                key={id}
                label={t(missionLabels[id][0], missionLabels[id][1])}
                value={duration(
                  average(
                    rows.flatMap((s) =>
                      s.missions
                        .filter((m) => m.id === id)
                        .map((m) => m.timeSeconds),
                    ),
                  ),
                )}
              />
            ))}
          </div>
        </Card>
      </div>
      <Card title={t("Interpreting results", "การตีความผล")}>
        <div className="padded guidance">
          <p>
            {t(
              "Session time and interaction counts describe observable gameplay. The meeting quiz measures recall of the briefing. These records cannot establish ADHD status or learning improvement.",
              "เวลาและจำนวนการโต้ตอบแสดงพฤติกรรมในเกม ส่วนแบบทดสอบวัดการจำเนื้อหาการประชุม ข้อมูลเหล่านี้ไม่สามารถใช้วินิจฉัย ADHD หรือยืนยันพัฒนาการเรียนรู้ได้",
            )}
          </p>
          <p>
            {t(
              "To evaluate learning, consider a separate, consented pre-activity and post-activity knowledge questionnaire with matched items and a defined scoring method.",
              "หากต้องการประเมินการเรียนรู้ อาจใช้แบบวัดความรู้ก่อนและหลังกิจกรรมโดยขอความยินยอมแยก ใช้ข้อคำถามที่เทียบกันได้และกำหนดวิธีให้คะแนนไว้ล่วงหน้า",
            )}
          </p>
        </div>
      </Card>
    </>
  );
}
