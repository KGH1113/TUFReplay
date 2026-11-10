using Microsoft.Data.Sqlite;

namespace TUFReplay.Activity.Migrations;

public static partial class ActivitySchema
{
  // Mod states saved with each run (ReplayApi): run_states holds each provider's value as the
  // run starts, run_state_events the values it recorded during the run. Added after version 1
  // shipped, so they are created in place: older builds still open the file, and their run
  // deletes still cascade here because the foreign keys live in these tables.
  private static void EnsureRunStateTables(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"CREATE TABLE IF NOT EXISTS run_state_shapes (
  shape_id TEXT PRIMARY KEY,
  shape_json TEXT NOT NULL,
  first_seen_at_utc TEXT NOT NULL
) WITHOUT ROWID;
CREATE TABLE IF NOT EXISTS run_states (
  run_id TEXT NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
  state_id TEXT NOT NULL,
  shape_id TEXT NOT NULL REFERENCES run_state_shapes(shape_id),
  value_json TEXT NOT NULL,
  saved_at_utc TEXT NOT NULL,
  PRIMARY KEY(run_id, state_id)
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_run_states_state ON run_states(state_id, shape_id);
CREATE TABLE IF NOT EXISTS run_state_events (
  run_id TEXT NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
  state_id TEXT NOT NULL,
  seq INTEGER NOT NULL,
  time_us INTEGER NOT NULL,
  shape_id TEXT NOT NULL REFERENCES run_state_shapes(shape_id),
  value_json TEXT NOT NULL,
  PRIMARY KEY(run_id, state_id, seq)
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_run_state_events_state ON run_state_events(state_id, shape_id);";
    command.ExecuteNonQuery();
  }
}
