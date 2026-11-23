"use strict";
const el = id => document.getElementById(id);
const words = {
  th: ["ผลกิจกรรมของคุณ","สรุปเฉพาะรอบที่สแกน ผู้ที่มีลิงก์นี้สามารถดูผลรอบนี้ได้","กรุณาสแกน QR จากหน้าสรุปในเกมอีกครั้ง","เวลาในกิจกรรม","ล้างจาน","จัดเก็บจาน","นำขยะไปทิ้ง","คำตอบที่ถูกต้อง","การโต้ตอบกับสิ่งรบกวน","สิ่งรบกวน","จำนวนครั้ง","วินาที","บันทึกรูป PNG","หากรูปยังไม่ถูกบันทึก ให้แตะรูปด้านล่างค้างไว้แล้วเลือกบันทึกภาพ"],
  en: ["Your activity results","This summary is visible to anyone with this round’s link.","Please scan the QR code on the game’s results screen again.","Activity duration","Wash dishes","Organize dishes","Take trash outside","Correct quiz answers","Distraction interactions","Distraction","Uses","Seconds","Save PNG","If the download did not start, press and hold the image below to save it."]
};
const names = {
  en: ["Fidget cube","Rainbow pop-it","Butter squishy","Television","Plushie","Phone","Neighbour chatter","Neighbour party","Construction site"],
  th: ["ฟิดเจ็ตคิวบ์","ป๊อปอิตสีรุ้ง","สกุชชี่เนย","โทรทัศน์","ตุ๊กตา","โทรศัพท์","เสียงเพื่อนบ้านคุยกัน","เสียงปาร์ตี้เพื่อนบ้าน","เสียงก่อสร้าง"]
};
const raw = location.hash.slice(1);
const fields = raw.split(".").map(value => value === "-" ? null : Number(value));
const version = fields[0];
const status = version === 2 ? fields[1] : 1;
const metrics = version === 2 ? fields.slice(2, 11) :
  [fields[1], fields[2], fields[4], fields[3], fields[4], ...fields.slice(5, 9)];
const validNumber = n => Number.isSafeInteger(n) && n >= 0;
const validPair = (done, total) => done == null || total == null || done <= total;
const rows = [];
if (version === 2) {
  for (let kind = 0; kind < 9; kind++) {
    const offset = 11 + kind * 2;
    rows.push([kind, fields[offset], fields[offset + 1]]);
  }
} else {
  for (let offset = 9; offset + 2 < fields.length && offset < 36; offset += 3)
    rows.push(fields.slice(offset, offset + 3));
}
const valid = raw.length < 512 && /^(?:(?:\d+|-)\.)*(?:\d+|-)$/.test(raw) &&
  (version === 2
    ? fields.length === 29 && [0, 1].includes(status) && fields.every(n => n === null || validNumber(n))
    : version === 1 && fields.length === 36 && fields.every(validNumber) &&
      rows.every(([kind]) => kind < 9) && new Set(rows.map(([kind]) => kind)).size === 9) &&
  [1, 3, 5, 7].every(index => validPair(metrics[index], metrics[index + 1]));
const measured = value => value == null ? "—" : value;
const ratio = (done, total) => done == null && total == null ? "—" : measured(done) + "/" + measured(total);
function append(parent, tag, value) {
  const node = document.createElement(tag);
  node.textContent = value;
  parent.append(node);
  return node;
}
function time(s) { return Math.floor(s/60) + ":" + String(s%60).padStart(2,"0"); }
function render() {
  const lang = el("lang").value, w = words[lang];
  document.documentElement.lang = lang;
  document.title = w[0] + " · Focus Shift";
  el("title").textContent = w[0];
  el("notice").textContent = valid ? w[1] : (lang === "th"
    ? "กรุณาสแกน QR ผลกิจกรรมจากหน้าเว็บอีกครั้ง"
    : "Please scan the result QR from the website again.");
  el("report").hidden = !valid;
  if (!valid) return;
  for (const id of ["missions","rows","head","hint"]) el(id).replaceChildren();
  el("preview").hidden = true;
  const [seconds, washed, dishes, organized, shelves, disposed, trash, correct, questions] = metrics;
  const values = [seconds == null ? "—" : time(seconds), ratio(washed, dishes),
    ratio(organized, shelves), ratio(disposed, trash), ratio(correct, questions)];
  const statusRow = append(el("missions"), "div", "");
  statusRow.className = "metric";
  append(statusRow, "span", lang === "th" ? "สถานะรอบ" : "Round status");
  append(statusRow, "b", status === 1
    ? (lang === "th" ? "เสร็จสิ้น" : "Completed")
    : (lang === "th" ? "จบก่อนครบภารกิจ" : "Interrupted"));
  values.forEach((value,i) => {
    const row = append(el("missions"), "div", "");
    row.className = "metric";
    append(row, "span", w[3+i]); append(row, "b", value);
  });
  el("interactions").textContent = w[8];
  const heading = append(el("head"),"tr","");
  w.slice(9,12).forEach(s => append(heading,"th",s));
  rows.forEach(([kind,count,tenths]) => {
    const row = append(el("rows"),"tr","");
    [names[lang][kind], measured(count), tenths == null ? "—" : (tenths/10).toFixed(1)]
      .forEach(v => append(row,"td",v));
  });
  el("save").textContent = w[12];
}
el("save").onclick = () => {
  const canvas = document.createElement("canvas");
  canvas.width = 1000; canvas.height = 1240;
  const g = canvas.getContext("2d");
  g.fillStyle = "#fff"; g.fillRect(0,0,canvas.width,canvas.height);
  function line(s,x,y,size,color="#172f46",align="left") {
    g.font = size+"px system-ui, sans-serif"; g.textAlign = align; g.fillStyle=color; g.fillText(s,x,y);
  }
  line("FOCUS SHIFT VR",56,82,30,"#176477");
  line(el("title").textContent,56,154,52);
  let y=240;
  for (const row of el("missions").children) {
    line(row.firstChild.textContent,56,y,34); line(row.lastChild.textContent,944,y,34,"#172f46","right"); y+=60;
  }
  line(el("interactions").textContent,56,598,40);
  for (const row of [...el("head").children,...el("rows").children]) {
    const rowY = 668 + row.sectionRowIndex * 54 + (row.parentNode === el("rows") ? 54 : 0);
    [...row.cells].forEach((cell,i) => line(cell.textContent,[56,680,944][i],rowY,31,"#172f46",i ? "right" : "left"));
  }
  const png = canvas.toDataURL("image/png");
  el("preview").src=png; el("preview").hidden=false;
  el("hint").textContent=words[el("lang").value][13];
  const link=document.createElement("a"); link.href=png; link.download="focus-shift-result.png";
  document.body.append(link); link.click(); link.remove();
};
el("lang").onchange = render;
window.addEventListener("hashchange", () => location.reload());
render();
