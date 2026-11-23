-- Keep the study notice associated with a saved run after its device token expires.
ALTER TABLE sessions ADD COLUMN consent_record_id INTEGER REFERENCES consent_records(id) ON DELETE SET NULL;
ALTER TABLE sessions ADD COLUMN consent_confirmed_at TEXT;
UPDATE sessions
SET (consent_record_id, consent_confirmed_at) = (
    SELECT consent_id, issued_at FROM upload_grants WHERE session_id = sessions.id
)
WHERE EXISTS (SELECT 1 FROM upload_grants WHERE session_id = sessions.id);
