CREATE TABLE game_starts (
  request_hash TEXT PRIMARY KEY NOT NULL,
  participant_id TEXT NOT NULL REFERENCES participants(id) ON DELETE CASCADE,
  sealed_pack TEXT,
  expires_at TEXT NOT NULL
);
CREATE INDEX game_start_expiry ON game_starts(expires_at);
CREATE INDEX game_start_participant ON game_starts(participant_id);
