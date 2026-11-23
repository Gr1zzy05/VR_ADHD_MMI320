CREATE TABLE game_pairings (
  code TEXT PRIMARY KEY NOT NULL,
  secret_hash TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  participant_id TEXT REFERENCES participants(id) ON DELETE CASCADE,
  sealed_pack TEXT
);
CREATE INDEX game_pairing_expiry ON game_pairings(expires_at);
CREATE INDEX game_pairing_participant ON game_pairings(participant_id);
