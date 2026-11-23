import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { date, kinds, missions } from "./data";
import type { Participant, Session } from "./data";
import { Modal } from "./management";
import { useLocale } from "./ui";

export function mobileResultUrl(session: Session, origin: string) {
  const url = new URL("/result.html", origin);
  // Keep the anonymous snapshot in a fragment so it is never sent in an HTTP request.
  const measurements = [
    session.durationSeconds == null ? null : Math.floor(session.durationSeconds),
    ...missions.slice(0, 3).flatMap((id) => {
      const mission = session.missions.find((row) => row.id === id);
      return [mission?.itemsDone, mission?.itemsTotal];
    }),
    session.quiz?.correct,
    session.quiz?.total,
    ...kinds.flatMap((kind) => {
      const row = session.distractions.find((item) => item.kind === kind);
      return [row?.count, row?.seconds == null ? null : Math.round(row.seconds * 10)];
    }),
  ];
  url.hash = [2, session.status === "completed" ? 1 : 0,
    ...measurements.map((value) => value ?? "-")].join(".");
  return url.href;
}

export function ResultShare({ participant, sessions, close }: {
  participant: Participant;
  sessions: Session[];
  close: () => void;
}) {
  const { lang, t } = useLocale();
  const [sessionId, setSessionId] = useState(sessions[0]?.id ?? "");
  const session = sessions.find((row) => row.id === sessionId);
  const url = session ? mobileResultUrl(session, window.location.origin) : "";
  const [qr, setQr] = useState<{ url: string; image: string }>();
  const [failedUrl, setFailedUrl] = useState("");
  useEffect(() => {
    if (!url) return;
    let current = true;
    QRCode.toDataURL(url, { width: 320, margin: 4, errorCorrectionLevel: "M" })
      .then((image) => { if (current) setQr({ url, image }); })
      .catch(() => { if (current) setFailedUrl(url); });
    return () => { current = false; };
  }, [url]);

  return (
    <Modal title={t("Result QR · ", "QR ผลกิจกรรม · ") + participant.code} close={close}>
      <label>
        {t("Activity round (UTC)", "รอบกิจกรรม (UTC)")}
        <select value={sessionId} onChange={(event) => setSessionId(event.target.value)}>
          {sessions.map((row, index) => (
            <option key={row.id} value={row.id}>
              {sessions.length - index} · {date(row.endedAt, lang, true)} · {row.status === "completed"
                ? t("Completed", "เสร็จสิ้น") : t("Interrupted", "จบก่อนครบภารกิจ")}
            </option>
          ))}
        </select>
      </label>
      <p>{t("Scan with a phone to view and save this round. Anyone with the QR or link can view the summary.",
        "ใช้กล้องมือถือสแกนเพื่อดูและบันทึกผลรอบนี้ ผู้ที่มี QR หรือลิงก์สามารถดูผลสรุปได้")}</p>
      {qr && qr.url === url && <img src={qr.image} width="320" height="320"
        style={{ display: "block", maxWidth: "100%", height: "auto", margin: "auto" }}
        alt={t("QR for this activity round", "QR สำหรับรอบกิจกรรมนี้")} />}
      {url && failedUrl === url && <p role="alert">{t("Could not generate the QR. Open the result link below.",
        "สร้าง QR ไม่สำเร็จ เปิดลิงก์ผลกิจกรรมด้านล่างได้")}</p>}
      {url && <a href={url} target="_blank" rel="noreferrer">{t("Open mobile result", "เปิดหน้าผลสำหรับมือถือ")}</a>}
    </Modal>
  );
}
