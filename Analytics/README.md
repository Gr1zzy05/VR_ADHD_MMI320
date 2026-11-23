# FOCUS SHIFT educational analytics

[คู่มือภาษาไทยสำหรับเปิดเว็บและเชื่อมเกม](docs/quickstart-th.md)

[คู่มือภาษาไทยสำหรับ Vercel, Neon PostgreSQL และบัญชีเจ้าหน้าที่](docs/vercel-th.md)

A bilingual staff dashboard for the HomeTasks Unity project. The service uses React, TypeScript, Vite, Recharts, Express and SQLite. The default installation is a single-process lab system with a local database. Its two configured staff accounts represent an administrator and a read-only educator.

## Run the demonstration

Use Node.js 24.12 or newer in the Node 24 series. From the Unity project root, run the following in PowerShell.

```powershell
cd Analytics
npm.cmd ci
npm.cmd run build
npm.cmd run demo
```

Open [the local demo](http://127.0.0.1:4311) and select a demo role. The demo starts with 18 fictional participants and 54 synthetic sessions. Changes made while exploring persist in `data/demo.synthetic.sqlite`. Demo credentials apply only to the separate loopback demo process. Keep real participants in the configured study service.

## Start a study database

1. From `Analytics`, copy `.env.example` to `.env`. Set `ADMIN_PASSWORD` and `EDUCATOR_PASSWORD` to distinct generated secrets of at least 16 characters. Configure account names, study retention and binding lifetime.
2. Run `npm.cmd run build`, then `npm.cmd start`. Open [the local staff dashboard](http://127.0.0.1:4310). The first start creates `data/analytics.sqlite` and applies numbered migrations. The study database starts empty.
3. To collect on a headset, configure the trusted HTTPS origin described below. `PUBLIC_ORIGIN` supplies the absolute URL inside each binding. The computer's loopback address is suitable for dashboard review only.
4. Configure `GAME_ENROLLMENT_KEY` on the HTTPS service. Generate 32 random bytes and encode them as base64url. Use the same key when building the game for supervised play.

For frontend development, set `PUBLIC_ORIGIN=http://127.0.0.1:5173`, keep the API on port 4310, run `npm.cmd start` and `npm.cmd run dev` in separate terminals, then use port 5173. Vite proxies the API to the study process. Use the built application behind HTTPS for supervised collection.

## Connect Unity

In the Unity Editor choose **Home Focus → Set study server for builds**. Enter the deployment's HTTPS origin and game enrollment key before building the Windows player or Quest APK. Keep the configured build within the supervised study. Its enrollment key can create anonymous study rounds and carries no staff data access. The configured resource is excluded from Git.

**Start and record results** confirms the in-game notice and reserves the next anonymous participant code and one session credential through `POST /api/game/start`. **Practice without sharing** skips enrollment and recording. The results screen stays open until the player presses **Start New Session**, which returns to the choice. Every recorded round started this way has a new participant code. The start of a recorded round requires internet access. Older builds need to be rebuilt to use this flow.

The component reads the completed HomeSessionResult or the current gameplay counters on interruption. A local checkpoint is refreshed every five seconds and on application pause. Normal completion, explicit restart and quit seal a packet. After a forced stop, the next gameplay launch recovers the latest durable observation as an interrupted session. Its end time is the time of that observation. Abrupt process or storage failure before the first checkpoint may leave no observation.

The outbox in `focus-shift-outbox` survives scene and process restarts. The uploader sends with UnityWebRequest in a coroutine, uses HTTPS without redirects, and retries transient errors with backoff. Resume the app online before the credential expires. The server accepts an identical retry once logically and rejects changed results using the same ID. The headset clock should be synchronized before pairing. Expired credentials stop delivery. Queued files may remain on a powered-off headset until its next launch. A device without connectivity cannot learn that consent has been withdrawn until it reconnects. For immediate local removal, close the app and remove that participant's queue files (named by session UUID).

## Review and reporting

| View | Contents |
| --- | --- |
| Overview | Participation totals, average activity time, task completion, quiz correctness, interactions and activity charts |
| Participant records | Search, sort, filters, registration and access to each coded profile |
| Participant detail | Registration and participation dates, repeated-session trends, session history and administrator controls |
| Session detail | UTC timestamps, game version, four missions, available item counts, meeting answers and nine distraction types |
| Behavior analytics | Measured interaction frequency and duration, completed-session duration, repeated participation and recall |
| Reports | Date, participant and completed-mission filters; printable report; session, mission, distraction and quiz CSV files |

An em dash denotes an unavailable value. CSV uses an empty cell. A measured zero is displayed and included in averages. Overall task completion divides completed mission rows by all recorded mission rows. Average activity duration includes interrupted sessions with a measured duration. The participant table's average completion duration uses completed sessions. Average quiz correctness is the unweighted mean of fully answered quiz percentages. Missing and partial quizzes do not contribute to that percentage. Incorrect answers exclude unanswered questions. Whole-session distraction totals can include overlapping interactions.

The time filters use session **start** date in UTC. The mission filter selects sessions in which the selected mission was completed. The mission dataset then returns only rows for that mission. The distraction, quiz and session exports still describe the entire selected sessions. The exports contain participant codes and session UUIDs and omit aliases. Treat these as coded research records when sharing because dates and repeated visits can be identifying in small cohorts.

## Operate a supervised installation

Serve the entire built application and API from the same origin. Choose a DNS name reachable from the staff computer and headset and a certificate trusted by both. Set `PUBLIC_ORIGIN=https://your-study-host.example` and keep the Node listener on `127.0.0.1:4310`. One example Caddyfile, replacing the example hostname:

```caddyfile
your-study-host.example {
    reverse_proxy 127.0.0.1:4310
}
```

Put the staff application behind your institution's network controls. Keep certificate validation enabled in Unity. Use a supervised service account, persist `DB_PATH` on local durable storage and restart the Node process after configuration changes. Keep credentials and the database outside the static `dist` directory and restrict their filesystem permissions. [Caddy HTTPS configuration](https://caddyserver.com/docs/automatic-https)

Staff sessions expire after eight hours and are invalidated on application restart. HTTPS deployments set secure, HttpOnly, SameSite cookies. The device has write access to one granted session and cannot read study records. Each payload is bounded in size and validated before a transactional insert. The database stores only a hash of the upload token. The server refuses submissions after expiry, withdrawal or participant deletion. Health or service monitoring should avoid recording bodies, bearer tokens and downloaded bindings.

The local defaults are 180 days for session retention and seven days for device submission. Set `RETENTION_DAYS` and `GRANT_DAYS` for the approved study before collecting data. The service prunes on startup and hourly. When all sessions have expired, it also removes old participant records with no recent consent or outstanding binding. Withdrawing consent disables new collection. Deleting a participant removes their sessions, child results, grants and consent history in the live database. Apply the same policy to exported files, device downloads and backup copies. Record and reapply outstanding deletion requests before bringing a restored backup online.

Use SQLite's online backup facility or `VACUUM INTO` to obtain a consistent snapshot. Protect backups with access controls and storage encryption, test restoration and set an expiry. [SQLite backup guidance](https://www.sqlite.org/backup.html)

This release is designed for a single service process and a bounded lab cohort. The authenticated dashboard loads that cohort into memory. A multi-site installation would need individual staff identity management, paginated reads and a database migration to PostgreSQL before scaling to multiple service instances.

## Verify

```powershell
# From Analytics
npm.cmd test
npm.cmd run build
# From the Unity project root, with its Library compiler cache available
powershell -ExecutionPolicy Bypass -File Tools/VerifyAnalyticsOutbox.ps1
# Optional native transport and database test, with a licensed Unity Editor and PowerShell 7
pwsh -File Tools/VerifyAnalyticsOutbox.ps1 -NetworkSmokeTest
```

The API suite uses temporary databases, two test roles and independent loopback HTTP clients. It covers automatic player codes, storage rollback, consent provenance, duplicate and conflicting retries, partial and zero-answer quizzes, filters, export and role boundaries. The C# verifier compiles the HomeTasks assembly and exercises queue operations against temporary files. The optional transport test uses an isolated Unity project and a loopback HTTPS API backed by temporary SQLite. It acquires two different player codes, disconnects the API, then verifies that UnityWebRequest delivers both rounds and handles a replay after reconnecting.

For a device acceptance test, select recording on a trusted HTTPS test instance. Disconnect the device after it gets a player code, finish the activities, then reconnect while the results are open. Compare the session ID, item totals, quiz and nine ledger rows with Unity. Start another recorded round and verify that it has a different code. Play a practice round and verify that the participant and session counts stay the same.

See the [measurement inventory, implementation plan and ER diagram](docs/design.md) and [API contract and example payload](docs/api.md).
