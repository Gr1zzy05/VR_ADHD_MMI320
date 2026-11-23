-- A participant can reach the questions and leave the session before answering.
ALTER TABLE quiz_results RENAME TO earlier_quiz_results;
CREATE TABLE quiz_results (
  session_id TEXT PRIMARY KEY NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  total INTEGER NOT NULL CHECK(total > 0),
  answered INTEGER NOT NULL CHECK(answered BETWEEN 0 AND total),
  correct INTEGER NOT NULL CHECK(correct BETWEEN 0 AND answered),
  incorrect INTEGER GENERATED ALWAYS AS (answered-correct) VIRTUAL,
  percentage REAL GENERATED ALWAYS AS (CASE WHEN answered=total THEN 100.0*correct/total END) VIRTUAL
) STRICT;
INSERT INTO quiz_results(session_id,total,answered,correct)
  SELECT session_id,total,answered,correct FROM earlier_quiz_results;
DROP TABLE earlier_quiz_results;
