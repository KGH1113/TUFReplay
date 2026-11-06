using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.RunState.Models;
using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.RunState.Repositories;

/// <summary>
/// Saves mod states with their run and reads them back. States are written in the run's own
/// transaction, and <c>run_states</c> cascades from <c>runs</c>, so deleting or locking a run
/// covers its states.
/// </summary>
internal static class RunStateRepository
{
  internal static void Insert(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string runId,
    IReadOnlyList<RunStateRecord> states
  )
  {
    if (states == null || states.Count == 0)
      return;
    using SqliteCommand shape = connection.CreateCommand();
    shape.Transaction = transaction;
    shape.CommandText =
      "INSERT OR IGNORE INTO run_state_shapes(shape_id,shape_json,first_seen_at_utc) VALUES(@shape,@json,@at)";
    SqliteParameter shapeId = shape.Parameters.Add("@shape", SqliteType.Text);
    SqliteParameter shapeJson = shape.Parameters.Add("@json", SqliteType.Text);
    SqliteParameter firstSeen = shape.Parameters.Add("@at", SqliteType.Text);

    using SqliteCommand state = connection.CreateCommand();
    state.Transaction = transaction;
    state.CommandText =
      @"INSERT OR REPLACE INTO run_states(run_id,state_id,shape_id,value_json,saved_at_utc)
VALUES(@run,@state,@shape,@value,@at)";
    state.Parameters.AddWithValue("@run", runId);
    SqliteParameter stateId = state.Parameters.Add("@state", SqliteType.Text);
    SqliteParameter stateShape = state.Parameters.Add("@shape", SqliteType.Text);
    SqliteParameter value = state.Parameters.Add("@value", SqliteType.Text);
    SqliteParameter savedAt = state.Parameters.Add("@at", SqliteType.Text);

    foreach (RunStateRecord record in states)
    {
      shapeId.Value = record.ShapeId;
      shapeJson.Value = record.ShapeJson;
      firstSeen.Value = record.SavedAtUtc;
      shape.ExecuteNonQuery();

      stateId.Value = record.StateId;
      stateShape.Value = record.ShapeId;
      value.Value = record.ValueJson;
      savedAt.Value = record.SavedAtUtc;
      state.ExecuteNonQuery();
    }
  }

  internal static List<StoredRunState> Load(string runId)
  {
    var states = new List<StoredRunState>();
    if (string.IsNullOrEmpty(runId))
      return states;
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"SELECT s.state_id,s.shape_id,h.shape_json,s.value_json
FROM run_states s
JOIN run_state_shapes h ON h.shape_id=s.shape_id
WHERE s.run_id=@run";
    command.Parameters.AddWithValue("@run", runId);
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
      states.Add(
        new StoredRunState
        {
          StateId = reader.GetString(0),
          ShapeId = reader.GetString(1),
          ShapeJson = reader.GetString(2),
          ValueJson = reader.GetString(3),
        }
      );
    return states;
  }

  internal static List<StoredRunStateShape> ListShapes(string stateId)
  {
    var shapes = new List<StoredRunStateShape>();
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"SELECT s.shape_id,h.shape_json,COUNT(*)
FROM run_states s
JOIN run_state_shapes h ON h.shape_id=s.shape_id
WHERE s.state_id=@state
GROUP BY s.shape_id,h.shape_json";
    command.Parameters.AddWithValue("@state", stateId);
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
      shapes.Add(
        new StoredRunStateShape
        {
          ShapeId = reader.GetString(0),
          ShapeJson = reader.GetString(1),
          RunCount = reader.GetInt32(2),
        }
      );
    return shapes;
  }
}
