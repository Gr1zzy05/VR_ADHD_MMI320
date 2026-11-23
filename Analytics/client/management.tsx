import { createContext, useContext, useEffect, useRef, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import {
  Download,
  FileSpreadsheet,
  LockKeyhole,
  Printer,
  ShieldCheck,
  X,
} from "lucide-react";
import {
  api,
  count,
  download,
  duration,
  missionLabels,
  missions,
  nonce,
  pct,
  stats,
} from "./data";
import type { Participant, RecordsData } from "./data";
import { Card, Header, SessionTable, SummaryLine, useLocale } from "./ui";

export const Policy = createContext<{
  retentionDays: number | null;
  grantDays: number | null;
  canProvision: boolean;
}>({ retentionDays: null, grantDays: null, canProvision: false });
export function Privacy() {
  const { t } = useLocale();
  const { retentionDays } = useContext(Policy);
  return (
    <div className="privacy">
      <h3>
        <ShieldCheck size={18} />
        {t(
          "Participant privacy notice",
          "ประกาศความเป็นส่วนตัวสำหรับผู้เข้าร่วม",
        )}
      </h3>
      <p>
        {t(
          "Participation is voluntary. You can play without recording a research session. With your permission, project staff use an anonymous code and optional nickname to link repeat visits for educational evaluation.",
          "การเข้าร่วมเป็นไปโดยสมัครใจ คุณสามารถเล่นโดยไม่บันทึกข้อมูลเพื่อการศึกษา หากคุณยินยอม เจ้าหน้าที่จะใช้รหัสนิรนามและชื่อเล่นที่เลือกกรอกเพื่อเชื่อมโยงการเข้าร่วมแต่ละครั้ง",
        )}
      </p>
      <p>
        {t(
          "We record session dates, activity duration, task progress, distraction counts and durations, and quiz responses as totals. Only authorized staff can review these records. Administrators can export coded data for academic reports.",
          "เราบันทึกวันเวลา ระยะเวลาของกิจกรรม ความคืบหน้าของภารกิจ จำนวนครั้งและระยะเวลาโต้ตอบกับสิ่งรบกวน รวมถึงจำนวนคำตอบแบบทดสอบ เฉพาะเจ้าหน้าที่ที่ได้รับอนุญาตเท่านั้นที่ตรวจดูข้อมูลได้ ผู้ดูแลระบบส่งออกข้อมูลที่ใช้รหัสสำหรับรายงานทางวิชาการได้",
        )}
      </p>
      <p>
        {retentionDays !== null &&
          t(
            "Session records are retained for " + retentionDays + " days. ",
            "ข้อมูลรอบกิจกรรมจัดเก็บเป็นเวลา " + retentionDays + " วัน ",
          )}
        {t(
          "To stop future uploads or request deletion of your history, tell the supervising educator or project administrator and provide your participant code.",
          "หากต้องการหยุดการส่งข้อมูลในอนาคตหรือขอลบประวัติ โปรดแจ้งรหัสผู้เข้าร่วมแก่ผู้สอนหรือผู้ดูแลโครงการ",
        )}
      </p>
      <small>
        {t("Notice version", "ประกาศฉบับ")} {nonce}
      </small>
    </div>
  );
}
export function Modal({
  title,
  close,
  children,
}: {
  title: string;
  close: () => void;
  children: ReactNode;
}) {
  const { t } = useLocale();
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const d = ref.current,
      prev = document.activeElement as HTMLElement | null;
    d?.showModal();
    return () => {
      d?.close();
      prev?.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      onCancel={(e) => {
        e.preventDefault();
        close();
      }}
      aria-labelledby="dialog-title"
    >
      <div className="modalhead">
        <h2 id="dialog-title">{title}</h2>
        <button
          className="iconbtn"
          aria-label={t("Close", "ปิด")}
          onClick={close}
        >
          <X size={20} />
        </button>
      </div>
      {children}
    </dialog>
  );
}
export function ResetPlayerCodes({ empty }: { empty: boolean }) {
  const { t } = useLocale();
  const [open, setOpen] = useState(false);
  const [confirmation, setConfirmation] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);
  const title = t("Restart at P001", "เริ่มนับจาก P001");

  async function reset(e: FormEvent) {
    e.preventDefault();
    if (busy || !empty || confirmation !== "P001") return;
    setBusy(true);
    setError("");
    try {
      await api("/api/participants/reset-codes", "POST", { confirmation });
      setOpen(false);
      setDone(true);
    } catch (error) {
      setError((error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <button
        className="secondary"
        onClick={() => {
          setConfirmation("");
          setError("");
          setDone(false);
          setOpen(true);
        }}
      >
        {title}
      </button>
      {done && empty && (
        <span role="status">
          {t(
            "The next participant will be P001.",
            "ผู้เข้าร่วมคนถัดไปจะได้รหัส P001",
          )}
        </span>
      )}
      {open && (
        <Modal
          title={title}
          close={() => {
            if (!busy) setOpen(false);
          }}
        >
          <form onSubmit={reset}>
            <p>
              {empty
                ? t(
                    "The next registration will use P001. Previously exported reports may contain the same code.",
                    "การลงทะเบียนครั้งถัดไปจะได้รหัส P001 รายงานที่ส่งออกไว้ก่อนหน้านี้อาจมีรหัสเดียวกัน",
                  )
                : t(
                    "This action is available when the participant directory is empty.",
                    "ใช้ปุ่มนี้ได้เมื่อทะเบียนผู้เข้าร่วมว่างเท่านั้น",
                  )}
            </p>
            {empty && (
              <label className="field">
                {t("Type P001 to confirm", "พิมพ์ P001 เพื่อยืนยัน")}
                <input
                  autoComplete="off"
                  value={confirmation}
                  onChange={(e) => setConfirmation(e.target.value)}
                />
              </label>
            )}
            {error && (
              <p className="error" role="alert">
                {error}
              </p>
            )}
            <div className="actions">
              <button
                type="button"
                className="secondary"
                disabled={busy}
                onClick={() => setOpen(false)}
              >
                {t("Close", "ปิด")}
              </button>
              {empty && (
                <button
                  className="primary"
                  disabled={busy || confirmation !== "P001"}
                >
                  {title}
                </button>
              )}
            </div>
          </form>
        </Modal>
      )}
    </>
  );
}

export function Registration({
  close,
  refresh,
}: {
  close: () => void;
  refresh: () => Promise<void>;
}) {
  const { t } = useLocale();
  const [nickname, set] = useState(""),
    [consent, agree] = useState(false),
    [busy, load] = useState(false),
    [error, fail] = useState("");
  async function save(e: FormEvent) {
    e.preventDefault();
    if (!consent || busy) return;
    load(true);
    fail("");
    try {
      const p = await api<Participant>("/api/participants", "POST", {
        nickname: nickname.trim() || null,
        consent: true,
        noticeVersion: nonce,
      });
      close();
      if (p?.id) location.hash = "/participants/" + p.id;
      await refresh();
    } catch (e) {
      fail(String((e as Error).message));
    } finally {
      load(false);
    }
  }
  return (
    <Modal
      title={t("Register participant", "ลงทะเบียนผู้เข้าร่วม")}
      close={() => {
        if (!busy) close();
      }}
    >
      <form onSubmit={save}>
        <label className="field">
          {t("Nickname (optional)", "ชื่อเล่น (ไม่จำเป็น)")}
          <input
            maxLength={40}
            value={nickname}
            onChange={(e) => set(e.target.value)}
            placeholder={t("Use an alias", "ใช้นามแฝง")}
          />
        </label>
        <Privacy />
        <label className="check">
          <input
            type="checkbox"
            required
            checked={consent}
            onChange={(e) => agree(e.target.checked)}
          />
          {t(
            "I shared this notice with the participant and they agreed to register and record activity.",
            "ฉันได้แจ้งรายละเอียดนี้แก่ผู้เข้าร่วม และผู้เข้าร่วมยินยอมให้ลงทะเบียนและบันทึกกิจกรรม",
          )}
        </label>
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        <div className="actions">
          <button
            className="secondary"
            type="button"
            onClick={close}
            disabled={busy}
          >
            {t("Cancel", "ยกเลิก")}
          </button>
          <button className="primary" disabled={!consent || busy}>
            {t("Create participant code", "สร้างรหัสผู้เข้าร่วม")}
          </button>
        </div>
      </form>
    </Modal>
  );
}
type Action = "withdraw" | "renew" | "delete";
export function ParticipantActions({
  p,
  refresh,
}: {
  p: Participant;
  refresh: () => Promise<void>;
}) {
  const { t } = useLocale();
  const [action, set] = useState<Action | null>(null),
    [checked, agree] = useState(false),
    [code, confirmCode] = useState(""),
    [busy, load] = useState(false),
    [err, fail] = useState(""),
    [note, msg] = useState("");
  const names = {
    withdraw: t("Withdraw consent", "ถอนความยินยอม"),
    renew: t("Record new consent", "บันทึกความยินยอมใหม่"),
    delete: t("Delete participant", "ลบผู้เข้าร่วม"),
  };
  function open(a: Action) {
    agree(false);
    confirmCode("");
    fail("");
    msg("");
    set(a);
  }
  async function apply(e: FormEvent) {
    e.preventDefault();
    if (!action || busy || !checked || (action === "delete" && code !== p.code))
      return;
    load(true);
    fail("");
    try {
      const base = "/api/participants/" + p.id;
      await api(
        base + (action === "delete" ? "" : "/consent"),
        action === "renew" ? "POST" : "DELETE",
        action === "renew"
          ? { noticeVersion: nonce, consent: true }
          : undefined,
      );
      await refresh();
      if (action === "delete") location.hash = "/participants";
      else msg(t("Consent record updated.", "อัปเดตข้อมูลความยินยอมแล้ว"));
      set(null);
    } catch (e) {
      fail((e as Error).message);
    } finally {
      load(false);
    }
  }
  return (
    <Card title={t("Participant administration", "จัดการข้อมูลผู้เข้าร่วม")}>
      <div className="padded">
        <div className="actions left">
          {p.consentedAt ? (
            <button className="secondary" onClick={() => open("withdraw")}>
              {names.withdraw}
            </button>
          ) : (
            <button className="secondary" onClick={() => open("renew")}>
              {names.renew}
            </button>
          )}
          <button className="danger ghost" onClick={() => open("delete")}>
            {names.delete}
          </button>
        </div>
        {note && <p role="status">{note}</p>}
      </div>
      {action && (
        <Modal
          title={names[action] + " · " + p.code}
          close={() => {
            if (!busy) set(null);
          }}
        >
          <form onSubmit={apply}>
            {action === "renew" ? (
              <Privacy />
            ) : (
              <p>
                {action === "withdraw"
                  ? t(
                      "Pending and future uploads for this participant will be refused. Existing sessions remain available for review.",
                      "ระบบจะปฏิเสธการส่งข้อมูลที่รออยู่และการส่งข้อมูลใหม่ แต่ยังสามารถตรวจดูรอบเดิมได้",
                    )
                  : t(
                      "This permanently deletes the participant, all sessions, consent history and pending upload permissions.",
                      "การดำเนินการนี้จะลบผู้เข้าร่วม ทุกรอบกิจกรรม ประวัติความยินยอม และสิทธิ์ส่งข้อมูลอย่างถาวร",
                    )}
              </p>
            )}
            {action === "delete" && (
              <label className="field">
                {t(
                  "Enter participant code to confirm",
                  "กรอกรหัสผู้เข้าร่วมเพื่อยืนยัน",
                )}
                <input
                  required
                  value={code}
                  onChange={(e) => confirmCode(e.target.value)}
                />
              </label>
            )}
            <label className="check">
              <input
                required
                type="checkbox"
                checked={checked}
                onChange={(e) => agree(e.target.checked)}
              />
              {action === "renew"
                ? t(
                    "The participant received the notice and gave fresh consent.",
                    "ผู้เข้าร่วมได้รับข้อมูลและได้ให้ความยินยอมใหม่",
                  )
                : t(
                    "I confirm this request for this participant.",
                    "ฉันยืนยันคำขอนี้สำหรับผู้เข้าร่วมรายนี้",
                  )}
            </label>
            {err && (
              <p role="alert" className="error">
                {err}
              </p>
            )}
            <div className="actions">
              <button
                type="button"
                className="secondary"
                disabled={busy}
                onClick={() => set(null)}
              >
                {t("Cancel", "ยกเลิก")}
              </button>
              <button
                className={action === "delete" ? "danger" : "primary"}
                disabled={
                  busy || !checked || (action === "delete" && code !== p.code)
                }
              >
                {names[action]}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </Card>
  );
}
export function Reports({
  data,
  admin,
}: {
  data: RecordsData;
  admin: boolean;
}) {
  const { t } = useLocale();
  const [from, setFrom] = useState(""),
    [to, setTo] = useState(""),
    [person, setPerson] = useState(""),
    [mission, setMission] = useState("");
  const [format, setFormat] = useState("sessions"),
    [err, fail] = useState(""),
    [busy, load] = useState(false);
  const invalid = Boolean(from && to && from > to);
  const rows = data.sessions.filter(
    (s) =>
      !invalid &&
      (!from || s.startedAt.slice(0, 10) >= from) &&
      (!to || s.startedAt.slice(0, 10) <= to) &&
      (!person || s.participantId === person) &&
      (!mission || s.missions.some((m) => m.id === mission && m.completed)),
  );
  const m = stats(rows);
  const participants =
    from || to || mission
      ? new Set(rows.map((s) => s.participantId)).size
      : data.participants.filter((p) => !person || p.id === person).length;
  const taskRate = mission ? (rows.length ? 100 : null) : m.completion;
  async function exportFile() {
    load(true);
    fail("");
    try {
      const query = new URLSearchParams({
        from,
        to,
        participant: person,
        mission,
        type: format,
      });
      const res = await fetch("/api/export?" + query);
      if (!res.ok)
        throw Error(
          t(
            "Export failed. Check your access and try again.",
            "ส่งออกไม่สำเร็จ โปรดตรวจสอบสิทธิ์และลองใหม่",
          ),
        );
      download(await res.blob(), "focus-shift-" + format + ".csv");
    } catch (e) {
      fail((e as Error).message);
    } finally {
      load(false);
    }
  }
  if (!admin)
    return (
      <>
        <Header
          title={t("Reports and export", "รายงานและส่งออกข้อมูล")}
          subtitle={t(
            "Administrator access required.",
            "จำเป็นต้องมีสิทธิ์ผู้ดูแลระบบ",
          )}
        />
        <EmptyRole />
      </>
    );
  return (
    <>
      <Header
        title={t("Reports and export", "รายงานและส่งออกข้อมูล")}
        subtitle={t(
          "Build an activity report and download anonymous research datasets.",
          "สร้างรายงานกิจกรรมและดาวน์โหลดชุดข้อมูลนิรนามเพื่อการศึกษา",
        )}
      >
        <button
          disabled={invalid}
          className="secondary"
          onClick={() => window.print()}
        >
          <Printer size={16} />
          {t("Print report", "พิมพ์รายงาน")}
        </button>
      </Header>
      <Card title={t("Report filters", "ตัวกรองรายงาน")}>
        <div className="report-filters">
          <label className="field">
            {t("From (UTC)", "ตั้งแต่ (UTC)")}
            <input
              type="date"
              value={from}
              max={to || undefined}
              onChange={(e) => setFrom(e.target.value)}
            />
          </label>
          <label className="field">
            {t("Through (UTC)", "ถึง (UTC)")}
            <input
              type="date"
              value={to}
              min={from || undefined}
              onChange={(e) => setTo(e.target.value)}
            />
          </label>
          <label className="field">
            {t("Participant", "ผู้เข้าร่วม")}
            <select value={person} onChange={(e) => setPerson(e.target.value)}>
              <option value="">
                {t("All participants", "ผู้เข้าร่วมทั้งหมด")}
              </option>
              {data.participants.map((p) => (
                <option value={p.id} key={p.id}>
                  {p.code}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            {t("Completed mission", "ภารกิจที่สำเร็จ")}
            <select
              value={mission}
              onChange={(e) => setMission(e.target.value)}
            >
              <option value="">{t("All missions", "ทุกภารกิจ")}</option>
              {missions.map((id) => (
                <option value={id} key={id}>
                  {t(missionLabels[id][0], missionLabels[id][1])}
                </option>
              ))}
            </select>
          </label>
        </div>
        {invalid && (
          <p className="error" role="alert">
            {t(
              "The end date must be on or after the start date.",
              "วันที่สิ้นสุดต้องตรงกับหรืออยู่หลังวันที่เริ่มต้น",
            )}
          </p>
        )}
      </Card>
      <div className="chart-grid">
        <Card title={t("Filtered summary", "สรุปผลตามตัวกรอง")}>
          <div className="padded">
            {[
              [t("Participants", "ผู้เข้าร่วม"), participants],
              [t("Sessions", "รอบกิจกรรม"), rows.length],
              [t("Completed sessions", "รอบที่เสร็จสิ้น"), m.completed],
              [
                t("Average duration (min:s)", "ระยะเวลาเฉลี่ย (นาที:วินาที)"),
                duration(m.duration),
              ],
              [t("Task completion", "ภารกิจที่สำเร็จ"), pct(taskRate)],
              [t("Average quiz correctness", "ตอบถูกเฉลี่ย"), pct(m.quiz)],
              [t("Recorded interactions", "การโต้ตอบที่บันทึก"), count(m.uses)],
            ].map(([label, v]) => (
              <SummaryLine key={label} label={String(label)} value={v} />
            ))}
          </div>
        </Card>
        <Card title={t("Anonymous CSV export", "ส่งออก CSV นิรนาม")}>
          <div className="padded export-box">
            <FileSpreadsheet size={35} />
            <p>
              {t(
                "Files use participant codes and preserve missing measurements as blank cells.",
                "ไฟล์ใช้รหัสผู้เข้าร่วมและเว้นช่องว่างสำหรับค่าที่ไม่มีการบันทึก",
              )}
            </p>
            <label className="field">
              {t("Dataset", "ชุดข้อมูล")}
              <select
                value={format}
                onChange={(e) => setFormat(e.target.value)}
              >
                {[
                  ["sessions", t("Session summaries", "สรุปรอบกิจกรรม")],
                  ["missions", t("Mission results", "ผลภารกิจ")],
                  ["distractions", t("Distraction results", "ผลการโต้ตอบ")],
                  ["quiz", t("Quiz results", "ผลแบบทดสอบ")],
                ].map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </select>
            </label>
            <button
              className="primary"
              onClick={exportFile}
              disabled={busy || invalid}
            >
              <Download size={16} />
              {t("Download CSV", "ดาวน์โหลด CSV")}
            </button>
            {err && (
              <p className="error" role="alert">
                {err}
              </p>
            )}
          </div>
        </Card>
      </div>
      <Card title={t("Included sessions", "รอบกิจกรรมในรายงาน")}>
        <SessionTable rows={rows} people={data.participants} />
      </Card>
    </>
  );
}
function EmptyRole() {
  const { t } = useLocale();
  return (
    <div className="empty">
      <LockKeyhole />
      {t(
        "Contact your project administrator to request an export.",
        "ติดต่อผู้ดูแลโครงการเพื่อขอส่งออกข้อมูล",
      )}
    </div>
  );
}
