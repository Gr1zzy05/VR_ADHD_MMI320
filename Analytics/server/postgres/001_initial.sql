CREATE TABLE participants (
  id TEXT PRIMARY KEY NOT NULL,
  sequence INTEGER NOT NULL UNIQUE,
  code TEXT NOT NULL UNIQUE,
  nickname TEXT,
  registered_at TEXT NOT NULL
);
CREATE TABLE code_sequence (value INTEGER NOT NULL CHECK(value >= 0));
INSERT INTO code_sequence VALUES (0);
CREATE TABLE consent_records (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  participant_id TEXT NOT NULL REFERENCES participants(id) ON DELETE CASCADE,
  notice_version TEXT NOT NULL,
  granted_at TEXT NOT NULL,
  withdrawn_at TEXT
);
CREATE UNIQUE INDEX active_consent ON consent_records(participant_id) WHERE withdrawn_at IS NULL;
CREATE TABLE upload_grants (
  session_id TEXT PRIMARY KEY NOT NULL,
  participant_id TEXT NOT NULL REFERENCES participants(id) ON DELETE CASCADE,
  consent_id INTEGER NOT NULL REFERENCES consent_records(id) ON DELETE CASCADE,
  token_hash TEXT UNIQUE NOT NULL,
  issued_at TEXT NOT NULL,
  expires_at TEXT NOT NULL
);
CREATE INDEX grant_expiry ON upload_grants(expires_at);
CREATE TABLE sessions (
  id TEXT PRIMARY KEY NOT NULL,
  participant_id TEXT NOT NULL REFERENCES participants(id) ON DELETE CASCADE,
  started_at TEXT NOT NULL,
  ended_at TEXT NOT NULL CHECK(ended_at >= started_at),
  status TEXT NOT NULL CHECK(status IN ('completed','interrupted')),
  duration_seconds DOUBLE PRECISION CHECK(duration_seconds >= 0),
  game_version TEXT NOT NULL,
  payload_hash TEXT NOT NULL,
  consent_record_id INTEGER REFERENCES consent_records(id) ON DELETE SET NULL,
  consent_confirmed_at TEXT
);
CREATE INDEX participant_history ON sessions(participant_id, started_at);
CREATE INDEX session_dates ON sessions(started_at);
CREATE INDEX session_retention ON sessions(ended_at);
CREATE TABLE mission_results (
  session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  mission_id TEXT NOT NULL CHECK(mission_id IN ('washing','organizing','trash','meeting')),
  completed INTEGER NOT NULL CHECK(completed IN (0,1)),
  items_done INTEGER CHECK(items_done >= 0),
  items_total INTEGER CHECK(items_total >= items_done),
  time_seconds DOUBLE PRECISION CHECK(time_seconds >= 0),
  focus_seconds DOUBLE PRECISION CHECK(focus_seconds >= 0),
  distraction_count INTEGER CHECK(distraction_count >= 0),
  PRIMARY KEY(session_id,mission_id)
);
CREATE INDEX mission_filter ON mission_results(mission_id,completed);
CREATE TABLE distraction_results (
  session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  kind TEXT NOT NULL CHECK(kind IN ('FidgetCube','PopIt','Squishy','Television','Plushie','Phone','Conversation','Party','Construction')),
  count INTEGER CHECK(count >= 0),
  seconds DOUBLE PRECISION CHECK(seconds >= 0),
  mission_id TEXT,
  PRIMARY KEY(session_id,kind),
  FOREIGN KEY(session_id,mission_id) REFERENCES mission_results(session_id,mission_id)
);
CREATE TABLE quiz_results (
  session_id TEXT PRIMARY KEY NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  total INTEGER NOT NULL CHECK(total > 0),
  answered INTEGER NOT NULL CHECK(answered BETWEEN 0 AND total),
  correct INTEGER NOT NULL CHECK(correct BETWEEN 0 AND answered),
  incorrect INTEGER GENERATED ALWAYS AS (answered-correct) STORED,
  percentage DOUBLE PRECISION GENERATED ALWAYS AS (CASE WHEN answered=total THEN 100.0::double precision*correct/total END) STORED
);
CREATE TABLE staff_logins (
  token_hash TEXT PRIMARY KEY,
  role TEXT NOT NULL CHECK(role IN ('admin','educator')),
  expires_at BIGINT NOT NULL
);
CREATE INDEX staff_expiry ON staff_logins(expires_at);
CREATE TABLE request_limits (
  key TEXT PRIMARY KEY,
  count INTEGER NOT NULL,
  until_at BIGINT NOT NULL
);
CREATE INDEX request_limit_expiry ON request_limits(until_at);
