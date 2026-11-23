CREATE TABLE participants (
  id TEXT PRIMARY KEY NOT NULL,
  sequence INTEGER NOT NULL UNIQUE,
  code TEXT NOT NULL UNIQUE,
  nickname TEXT,
  registered_at TEXT NOT NULL
) STRICT;
CREATE TABLE code_sequence (value INTEGER NOT NULL CHECK(value >= 0)) STRICT;
INSERT INTO code_sequence VALUES (0);
CREATE TABLE consent_records (
  id INTEGER PRIMARY KEY,
  participant_id TEXT NOT NULL REFERENCES participants(id) ON DELETE CASCADE,
  notice_version TEXT NOT NULL,
  granted_at TEXT NOT NULL,
  withdrawn_at TEXT
) STRICT;
CREATE UNIQUE INDEX active_consent ON consent_records(participant_id) WHERE withdrawn_at IS NULL;
CREATE TABLE upload_grants (
  session_id TEXT PRIMARY KEY NOT NULL,
  participant_id TEXT NOT NULL REFERENCES participants(id) ON DELETE CASCADE,
  consent_id INTEGER NOT NULL REFERENCES consent_records(id) ON DELETE CASCADE,
  token_hash TEXT UNIQUE NOT NULL,
  issued_at TEXT NOT NULL,
  expires_at TEXT NOT NULL
) STRICT;
CREATE INDEX grant_expiry ON upload_grants(expires_at);
CREATE TABLE sessions (
  id TEXT PRIMARY KEY NOT NULL,
  participant_id TEXT NOT NULL REFERENCES participants(id) ON DELETE CASCADE,
  started_at TEXT NOT NULL,
  ended_at TEXT NOT NULL CHECK(ended_at >= started_at),
  status TEXT NOT NULL CHECK(status IN ('completed','interrupted')),
  duration_seconds REAL CHECK(duration_seconds >= 0),
  game_version TEXT NOT NULL,
  payload_hash TEXT NOT NULL
) STRICT;
CREATE INDEX participant_history ON sessions(participant_id, started_at);
CREATE INDEX session_dates ON sessions(started_at);
CREATE INDEX session_retention ON sessions(ended_at);
CREATE TABLE mission_results (
  session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  mission_id TEXT NOT NULL CHECK(mission_id IN ('washing','organizing','trash','meeting')),
  completed INTEGER NOT NULL CHECK(completed IN (0,1)),
  items_done INTEGER CHECK(items_done >= 0),
  items_total INTEGER CHECK(items_total >= items_done),
  time_seconds REAL CHECK(time_seconds >= 0),
  focus_seconds REAL CHECK(focus_seconds >= 0),
  distraction_count INTEGER CHECK(distraction_count >= 0),
  PRIMARY KEY(session_id,mission_id)
) STRICT;
CREATE INDEX mission_filter ON mission_results(mission_id,completed);
CREATE TABLE distraction_results (
  session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  kind TEXT NOT NULL CHECK(kind IN ('FidgetCube','PopIt','Squishy','Television','Plushie','Phone','Conversation','Party','Construction')),
  count INTEGER CHECK(count >= 0),
  seconds REAL CHECK(seconds >= 0),
  mission_id TEXT,
  PRIMARY KEY(session_id,kind),
  FOREIGN KEY(session_id,mission_id) REFERENCES mission_results(session_id,mission_id)
) STRICT;
CREATE TABLE quiz_results (
  session_id TEXT PRIMARY KEY NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  total INTEGER NOT NULL CHECK(total > 0),
  answered INTEGER NOT NULL CHECK(answered BETWEEN 1 AND total),
  correct INTEGER NOT NULL CHECK(correct BETWEEN 0 AND answered),
  incorrect INTEGER GENERATED ALWAYS AS (answered-correct) VIRTUAL,
  percentage REAL GENERATED ALWAYS AS (CASE WHEN answered=total THEN 100.0*correct/total END) VIRTUAL
) STRICT;
