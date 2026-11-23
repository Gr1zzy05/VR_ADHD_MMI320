# HTTP contract v1

All paths start with `/api`. Staff requests use an HttpOnly SameSite=Strict session cookie. Submit JSON using Content-Type application/json. State-changing browser requests are same-origin. Administrators can manage records and export. Educators can read records and aggregates. UTC ISO timestamps include timezone. Date filters use inclusive UTC calendar dates.

| Method and path | Input / result |
| --- | --- |
| GET /config | Public demo-mode boolean only |
| POST /login | username, password → role and demo flag |
| GET /me, POST /logout | Current staff role / invalidate current cookie |
| GET /records | Authorized participant list, session summaries and measured children |
| GET /summary | Filtered aggregates. Same filters as export |
| POST /participants | Optional nickname, consent=true and noticeVersion=2026-01. Returns anonymous UUID and generated code |
| POST /participants/:id/consent | Record a new consent after withdrawal |
| DELETE /participants/:id/consent | Withdraw future collection, invalidate outstanding credentials |
| POST /participants/:id/binding | consent=true and noticeVersion=2026-01 after permission for the next run. Download a one-session device credential |
| POST /participants/:id/device | Administrator. consent=true, noticeVersion=2026-01 and runs=1–30. Returns the participant code and a batch of one-use bindings as focus-shift-device.json. The server still validates each submission independently. |
| POST /game/pair | Built game requests a pairing code and private polling secret over HTTPS. Empty object. Returns 201 with code, secret, and expiry. |
| POST /game/start | Supervised build sends its enrollment bearer key, requestId (UUID), noticeVersion, and consent=true. Returns a fresh anonymous participant code and one session credential. An identical retry gets the same result within the start request window. Practice never calls this endpoint. |
| GET /game/pair/:code | Bearer polling secret. Returns 204 until assigned, then 200 with a pack of one-use bindings. |
| POST /participants/:id/pair | Administrator enters the code shown by the game, sends consent=true, noticeVersion=2026-01 and runs=1–30. Returns 204 after authorizing the device for this participant. |
| DELETE /participants/:id | Delete participant, consent, grants and all child records |
| POST /submissions | Device bearer token only. 201 for saved, 200 for identical retry, 409 for conflicting replay |
| GET /export | Anonymous CSV. type=sessions, missions, distractions or quiz. Optional from, to, participant (UUID) and mission (completed mission ID). Mission rows are also restricted to that mission; quiz and distraction rows retain their session scope |

Example Unity submission (illustrative abbreviated distraction ledger):

```json
{
  "id":"02020202-2222-4222-8222-222222222222",
  "participantId":"01010101-1111-4111-8111-111111111111",
  "startedAt":"2026-11-22T03:10:00.000Z",
  "endedAt":"2026-11-22T03:11:00.000Z",
  "status":"interrupted",
  "durationSeconds":57.6,
  "gameVersion":"1.0",
  "missions":[
    {"id":"washing","completed":false,"itemsDone":2,"itemsTotal":6},
    {"id":"organizing","completed":false,"itemsDone":0,"itemsTotal":6},
    {"id":"trash","completed":false,"itemsDone":0,"itemsTotal":3},
    {"id":"meeting","completed":false}
  ],
  "distractions":[{"kind":"Phone","count":1,"seconds":2.4}],
  "quiz":null
}
```

The game sends all nine measured ledger rows. A missing type has no observation. A quiz object contains total, answered and correct. Reaching the questions before answering records an answered count of zero. An unreached or reset attempt has no quiz row. Incorrect equals answered minus correct. Percentage is calculated for a fully answered quiz only. Session duration uses the game's scaled timer, independent of wall-clock duration. Unsupported mission measurements are returned as null. Exported empty cells represent missing measurements.
