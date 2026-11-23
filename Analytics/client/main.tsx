import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { createRoot } from "react-dom/client";
import {
  Activity,
  BarChart3,
  ChevronRight,
  Droplets,
  Eye,
  EyeOff,
  FileBarChart2,
  Globe2,
  GraduationCap,
  Home,
  LayoutDashboard,
  Layers3,
  LockKeyhole,
  LogOut,
  Menu,
  MonitorPlay,
  RefreshCw,
  ShieldCheck,
  Trash2,
  Users,
  Utensils,
  X,
} from "lucide-react";
import { api } from "./data";
import type { Lang, RecordsData, User } from "./data";
import {
  Behavior,
  Overview,
  ParticipantDetail,
  Participants,
  Reports,
  SessionDetail,
  Sessions,
} from "./pages";
import { Brand, Empty, Locale, useLocale } from "./ui";
import { Policy, Privacy } from "./management";
import "./style.css";

type Config = {
  demo: boolean;
  retentionDays: number | null;
  grantDays: number | null;
  canProvision: boolean;
};
const initialConfig: Config = {
  demo: false,
  retentionDays: null,
  grantDays: null,
  canProvision: false,
};
function Language({ lang, set }: { lang: Lang; set: (l: Lang) => void }) {
  return (
    <label className="language">
      <Globe2 size={16} />
      <select
        aria-label={lang === "en" ? "Language" : "ภาษา"}
        value={lang}
        onChange={(e) => set(e.target.value as Lang)}
      >
        <option value="en">English</option>
        <option value="th">ไทย</option>
      </select>
    </label>
  );
}
function SignIn({
  cfg,
  success,
  changeLang,
}: {
  cfg: Config;
  success: () => Promise<void>;
  changeLang: (l: Lang) => void;
}) {
  const { lang, t } = useLocale();
  const [user, setUser] = useState(""),
    [pwd, setPwd] = useState(""),
    [visible, show] = useState(false),
    [busy, load] = useState(false),
    [error, fail] = useState(""),
    [privacy, setPrivacy] = useState(false);
  async function login(username: string, password: string) {
    if (busy) return;
    load(true);
    fail("");
    try {
      await api("/api/login", "POST", { username, password });
      await success();
    } catch {
      fail(
        t(
          "Sign-in failed. Check your credentials and the server connection.",
          "เข้าสู่ระบบไม่สำเร็จ โปรดตรวจสอบข้อมูลบัญชีและการเชื่อมต่อเซิร์ฟเวอร์",
        ),
      );
    } finally {
      load(false);
    }
  }
  function submit(e: FormEvent) {
    e.preventDefault();
    void login(user, pwd);
  }
  return (
    <div className="auth">
      <header className="auth-top">
        <Brand />
        <Language lang={lang} set={changeLang} />
      </header>
      <main className="auth-layout">
        <section className="auth-form">
          <div className="auth-context">
            <ShieldCheck size={16} />
            {t("Staff workspace", "พื้นที่ทำงานของเจ้าหน้าที่")}
          </div>
          <h1>{t("Welcome back", "ยินดีต้อนรับกลับ")}</h1>
          <p className="lede">
            {t(
              "Sign in to review participation and activity results from the Focus Shift VR simulation.",
              "เข้าสู่ระบบเพื่อดูการเข้าร่วมและผลกิจกรรมจากการจำลอง VR ของ Focus Shift",
            )}
          </p>
          <form onSubmit={submit}>
            <label className="field">
              {t("Username", "ชื่อผู้ใช้")}
              <input
                required
                autoComplete="username"
                value={user}
                onChange={(e) => setUser(e.target.value)}
                placeholder={t("Enter your username", "กรอกชื่อผู้ใช้")}
              />
            </label>
            <label className="field">
              {t("Password", "รหัสผ่าน")}
              <span className="pass">
                <input
                  required
                  autoComplete="current-password"
                  type={visible ? "text" : "password"}
                  value={pwd}
                  onChange={(e) => setPwd(e.target.value)}
                  placeholder={t("Enter your password", "กรอกรหัสผ่าน")}
                />
                <button
                  aria-label={
                    visible
                      ? t("Hide password", "ซ่อนรหัสผ่าน")
                      : t("Show password", "แสดงรหัสผ่าน")
                  }
                  type="button"
                  onClick={() => show(!visible)}
                >
                  {visible ? <EyeOff size={17} /> : <Eye size={17} />}
                </button>
              </span>
            </label>
            {error && (
              <p className="error" role="alert">
                {error}
              </p>
            )}
            <button disabled={busy} className="primary signin">
              {busy
                ? t("Signing in…", "กำลังเข้าสู่ระบบ…")
                : t("Sign in", "เข้าสู่ระบบ")}
            </button>
          </form>
          {cfg.demo && (
            <div className="demo-entry">
              <div>
                <span className="demo-dot" />
                <strong>{t("Demo workspace", "พื้นที่สาธิต")}</strong>
              </div>
              <p>
                {t(
                  "Explore the dashboard with clearly labeled mock records.",
                  "สำรวจแดชบอร์ดด้วยข้อมูลจำลองที่ระบุไว้อย่างชัดเจน",
                )}
              </p>
              <div className="demo-buttons">
                <button
                  disabled={busy}
                  className="secondary"
                  onClick={() => login("demo-admin", "focus-shift-demo")}
                >
                  {t("Demo administrator", "ผู้ดูแลระบบสาธิต")}
                </button>
                <button
                  disabled={busy}
                  className="secondary"
                  onClick={() => login("demo-educator", "focus-shift-demo")}
                >
                  {t("Demo educator", "ผู้สอนสาธิต")}
                </button>
              </div>
            </div>
          )}
        </section>
        <aside className="auth-art">
          <div className="art-caption">
            <Activity size={18} />
            <span>FOCUS SHIFT VR</span>
          </div>
          <h2>
            {t(
              "Educational activity analytics",
              "การวิเคราะห์กิจกรรมการเรียนรู้",
            )}
          </h2>
          <p>
            {t(
              "Follow a participant’s journey across four everyday activities.",
              "ติดตามการเข้าร่วมผ่านกิจกรรมในชีวิตประจำวันทั้งสี่ภารกิจ",
            )}
          </p>
          <div className="simulation-map" aria-hidden="true">
            <div className="orbit orbit1" />
            <div className="orbit orbit2" />
            <div className="orbit orbit3" />
            <div className="map-center">
              <img
                width="80"
                height="80"
                src={new URL("./mark.svg", import.meta.url).href}
                alt=""
              />
            </div>
            {[
              [Droplets, "wash", t("Wash dishes", "ล้างจาน")],
              [Utensils, "organize", t("Organize dishes", "จัดเก็บจาน")],
              [Trash2, "trash", t("Take out trash", "นำขยะไปทิ้ง")],
              [
                MonitorPlay,
                "meeting",
                t("Meeting + quiz", "ประชุมและตอบคำถาม"),
              ],
            ].map(([G, pos, label]) => {
              const Icon = G as typeof Droplets;
              return (
                <div key={String(pos)} className={"map-node " + pos}>
                  <Icon size={19} />
                  {String(label)}
                </div>
              );
            })}
          </div>
          <div className="art-bottom">
            <Layers3 size={18} />
            {t(
              "Session-level activity records",
              "ข้อมูลกิจกรรมระดับรอบการเข้าร่วม",
            )}
          </div>
        </aside>
      </main>
      <footer className="auth-footer">
        <span>
          <GraduationCap size={17} />
          {t("Educational research workspace", "พื้นที่งานวิจัยด้านการศึกษา")}
        </span>
        <button onClick={() => setPrivacy(!privacy)}>
          <ShieldCheck size={15} />
          {t("Privacy notice", "ประกาศความเป็นส่วนตัว")}
        </button>
      </footer>
      {privacy && (
        <div className="auth-notice">
          <Privacy />
        </div>
      )}
    </div>
  );
}
function App() {
  const [lang, setLang] = useState<Lang>(() =>
    localStorage.getItem("focus-shift-lang") === "th" ? "th" : "en",
  );
  const t = (a: string, b: string) => (lang === "en" ? a : b);
  const [user, setUser] = useState<User | null>(null),
    [config, setConfig] = useState(initialConfig),
    [data, setData] = useState<RecordsData | null>(null),
    [pending, setPending] = useState(true),
    [error, fail] = useState(""),
    [route, navigate] = useState(location.hash.slice(1) || "/"),
    [mobile, open] = useState(false),
    [loading, setLoading] = useState(false),
    [privacy, setPrivacy] = useState(false);
  const changeLang = (v: Lang) => {
    setLang(v);
    localStorage.setItem("focus-shift-lang", v);
  };
  useEffect(() => {
    document.documentElement.lang = lang;
  }, [lang]);
  useEffect(() => {
    const f = () => {
      navigate(location.hash.slice(1) || "/");
      open(false);
      setPrivacy(false);
      window.scrollTo(0, 0);
    };
    window.addEventListener("hashchange", f);
    return () => window.removeEventListener("hashchange", f);
  }, []);
  const latestRequest = useRef(0);
  const refresh = useCallback(async () => {
    const request = ++latestRequest.current;
    setLoading(true);
    try {
      const response = await fetch("/api/records", { cache: "no-store" });
      if (request !== latestRequest.current) return;
      if (response.status === 401) {
        setUser(null);
        setData(null);
        return;
      }
      if (!response.ok) throw Error(String(response.status));
      const records = (await response.json()) as RecordsData;
      if (request !== latestRequest.current) return;
      setData(records);
      fail("");
    } catch (e) {
      if (request !== latestRequest.current) return;
      fail((e as Error).message);
      throw e;
    } finally {
      if (request === latestRequest.current) setLoading(false);
    }
  }, []);
  const identify = useCallback(async () => {
    const profile = await fetch("/api/me", { cache: "no-store" });
    if (profile.status === 401) {
      setUser(null);
      setData(null);
      return;
    }
    if (!profile.ok) throw Error(String(profile.status));
    const identity = (await profile.json()) as User & Config;
    setUser(identity);
    setConfig(identity);
    await refresh();
  }, [refresh]);
  const init = useCallback(async () => {
    setPending(true);
    fail("");
    try {
      setConfig(await api<Config>("/api/config"));
      await identify();
    } catch (e) {
      fail((e as Error).message);
    } finally {
      setPending(false);
    }
  }, [identify]);
  useEffect(() => {
    void init();
  }, [init]);
  useEffect(() => {
    if (!user) return;
    const sync = () => {
      if (document.visibilityState === "visible")
        void refresh().catch(() => {});
    };
    window.addEventListener("focus", sync);
    const timer = window.setInterval(sync, 60000);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", sync);
    };
  }, [user, refresh]);
  async function logout() {
    try {
      await api("/api/logout", "POST", {});
      latestRequest.current++;
      setLoading(false);
      setUser(null);
      setData(null);
      fail("");
      location.hash = "/";
    } catch (e) {
      fail((e as Error).message);
    }
  }
  const nav = [
    { path: "/", name: t("Overview", "ภาพรวม"), icon: LayoutDashboard },
    {
      path: "/participants",
      name: t("Participants", "ผู้เข้าร่วม"),
      icon: Users,
    },
    {
      path: "/sessions",
      name: t("Session records", "รอบกิจกรรม"),
      icon: Layers3,
    },
    {
      path: "/analytics",
      name: t("Behavior analytics", "วิเคราะห์พฤติกรรม"),
      icon: BarChart3,
    },
    ...(user?.role === "admin"
      ? [
          {
            path: "/reports",
            name: t("Reports and export", "รายงานและส่งออก"),
            icon: FileBarChart2,
          },
        ]
      : []),
  ];
  const base = "/" + (route.split("/")[1] ?? "");
  const id = route.split("/")[2];
  const active = nav.find((n) => n.path === base);
  const role =
    user?.role === "admin"
      ? t("Administrator", "ผู้ดูแลระบบ")
      : t("Educator", "ผู้สอน");
  const currentPage = () => {
    if (!user || !data) return null;
    if (id && base === "/participants") {
      const p = data.participants.find((p) => p.id === id);
      if (p)
        return (
          <ParticipantDetail
            key={id}
            data={data}
            p={p}
            admin={user.role === "admin"}
            refresh={refresh}
          />
        );
    } else if (id && base === "/sessions") {
      const s = data.sessions.find((s) => s.id === id);
      if (s)
        return (
          <SessionDetail
            s={s}
            p={data.participants.find((p) => p.id === s.participantId)}
          />
        );
    } else if (!id) {
      if (base === "/") return <Overview data={data} />;
      if (base === "/participants")
        return (
          <Participants
            data={data}
            admin={user.role === "admin"}
            refresh={refresh}
          />
        );
      if (base === "/sessions") return <Sessions data={data} />;
      if (base === "/analytics") return <Behavior data={data} />;
      if (base === "/reports")
        return <Reports data={data} admin={user.role === "admin"} />;
    }
    return (
      <Empty>
        {t("Record or page unavailable", "ไม่พบข้อมูลหรือหน้าที่ต้องการ")}
        <a href="#/">{t("Return to overview", "กลับไปหน้าภาพรวม")}</a>
      </Empty>
    );
  };
  return (
    <Locale.Provider value={{ lang, t }}>
      <Policy.Provider value={config}>
        {pending ? (
          <div role="status" className="startup">
            <Brand />
            <p>{t("Opening workspace…", "กำลังเปิดพื้นที่ทำงาน…")}</p>
          </div>
        ) : !user && error ? (
          <div className="startup">
            <Brand />
            <p>
              {t(
                "The workspace service is unavailable.",
                "ไม่สามารถเชื่อมต่อบริการได้",
              )}
            </p>
            <button className="primary" onClick={() => void init()}>
              {t("Try again", "ลองใหม่")}
            </button>
          </div>
        ) : !user ? (
          <SignIn cfg={config} success={identify} changeLang={changeLang} />
        ) : (
          <div className="workspace">
            {mobile && (
              <button
                className="scrim"
                aria-label={t("Close navigation", "ปิดเมนู")}
                onClick={() => open(false)}
              />
            )}
            <aside className={"sidebar " + (mobile ? "show" : "")}>
              <div className="sidebar-brand">
                <Brand />
                <span>{t("Research workspace", "พื้นที่วิจัย")}</span>
              </div>
              <div className="nav-label">{t("Workspace", "พื้นที่ทำงาน")}</div>
              <nav>
                {nav.map((n) => (
                  <a
                    key={n.path}
                    href={"#" + n.path}
                    className={
                      base === n.path ? "nav-item selected" : "nav-item"
                    }
                    aria-current={base === n.path ? "page" : undefined}
                  >
                    <n.icon size={19} strokeWidth={1.85} />
                    {n.name}
                  </a>
                ))}
              </nav>
              <div className="side-bottom">
                <button
                  className="nav-item"
                  onClick={() => {
                    setPrivacy(!privacy);
                    open(false);
                  }}
                >
                  <ShieldCheck size={18} />
                  {t("Privacy notice", "ความเป็นส่วนตัว")}
                </button>
                <div className="identity">
                  <span className="id-icon">
                    <GraduationCap size={20} />
                  </span>
                  <div>
                    <b>{role}</b>
                    <small>
                      {user.demo
                        ? t("Mock dataset", "ชุดข้อมูลจำลอง")
                        : t("Staff access", "สิทธิ์เจ้าหน้าที่")}
                    </small>
                  </div>
                  <button
                    className="iconbtn"
                    onClick={logout}
                    aria-label={t("Sign out", "ออกจากระบบ")}
                    title={t("Sign out", "ออกจากระบบ")}
                  >
                    <LogOut size={16} />
                  </button>
                </div>
              </div>
            </aside>
            <div className="main-column">
              <header className="topbar">
                <div className="crumb">
                  <button
                    className="iconbtn menu"
                    aria-label={t("Open navigation", "เปิดเมนู")}
                    onClick={() => open(!mobile)}
                  >
                    <Menu size={21} />
                  </button>
                  <span className="crumb-home">
                    <Home size={15} />
                    <span>{t("Workspace", "พื้นที่ทำงาน")}</span>
                    <ChevronRight size={14} />
                  </span>
                  <strong>{active?.name ?? t("Records", "ข้อมูล")}</strong>
                  {id && (
                    <>
                      <ChevronRight size={14} />
                      <span>{t("Details", "รายละเอียด")}</span>
                    </>
                  )}
                </div>
                <div className="top-actions">
                  {user.demo && (
                    <span className="demo-tag">
                      <i />
                      {t("Mock data", "ข้อมูลจำลอง")}
                    </span>
                  )}
                  <Language lang={lang} set={changeLang} />
                </div>
              </header>
              <main className="content">
                {error && (
                  <div className="error-banner" role="alert">
                    {t(
                      "Unable to refresh the records.",
                      "ไม่สามารถดึงข้อมูลล่าสุดได้",
                    )}{" "}
                    <button onClick={() => void refresh().catch(() => {})}>
                      {t("Try again", "ลองใหม่")}
                    </button>
                  </div>
                )}
                {privacy && (
                  <div className="notice-panel">
                    <button
                      className="iconbtn"
                      aria-label={t("Close notice", "ปิดประกาศ")}
                      onClick={() => setPrivacy(false)}
                    >
                      <X size={18} />
                    </button>
                    <Privacy />
                  </div>
                )}
                {currentPage()}
              </main>
              <footer className="workspace-footer">
                <span>
                  <LockKeyhole size={13} />
                  {t(
                    "Authorized educational use",
                    "สำหรับการศึกษาที่ได้รับอนุญาต",
                  )}
                </span>
                <button
                  disabled={loading}
                  onClick={() => void refresh().catch(() => {})}
                >
                  <RefreshCw size={13} />
                  {loading
                    ? t("Refreshing…", "กำลังโหลด…")
                    : t("Refresh data", "โหลดข้อมูลล่าสุด")}
                </button>
              </footer>
            </div>
          </div>
        )}
      </Policy.Provider>
    </Locale.Provider>
  );
}
createRoot(document.getElementById("root")!).render(<App />);
