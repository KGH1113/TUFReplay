using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using TUFReplay.ModApi.Models;
using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.ModApi.Repositories;

/// <summary>
/// Saves mod states with their run and reads them back. States are written in the run's own
/// transaction, and <c>run_states</c> and <c>run_state_events</c> cascade from <c>runs</c>, so
/// deleting or locking a run covers its states.
/// </summary>
internal static class RunStateRepository
{
  internal static void Insert(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string runId,
    CapturedRunStates captured
  )
  {
    if (captured == null || captured.IsEmpty)
      return;
    using SqliteCommand shape = connection.CreateCommand();
    shape.Transaction = transaction;
    shape.CommandText =
      "INSERT OR IGNORE INTO run_state_shapes(shape_id,shape_json,first_seen_at_utc) VALUES(@shape,@json,@at)";
    SqliteParameter shapeId = shape.Parameters.Add("@shape", SqliteType.Text);
    SqliteParameter shapeJson = shape.Parameters.Add("@json", SqliteType.Text);
    SqliteParameter firstSeen = shape.Parameters.Add("@at", SqliteType.Text);
    string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    var savedShapes = new HashSet<string>(StringComparer.Ordinal);

    void SaveShape(string id, string json, string seenAt)
    {
      if (!savedShapes.Add(id))
        return;
      shapeId.Value = id;
      shapeJson.Value = json;
      firstSeen.Value = seenAt;
      shape.ExecuteNonQuery();
    }

    if (captured.States.Count > 0)
    {
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
      foreach (RunStateRecord record in captured.States)
      {
        SaveShape(record.ShapeId, record.ShapeJson, record.SavedAtUtc);
        stateId.Value = record.StateId;
        stateShape.Value = record.ShapeId;
        value.Value = record.ValueJson;
        savedAt.Value = record.SavedAtUtc;
        state.ExecuteNonQuery();
      }
    }

    if (captured.Events.Count > 0)
    {
      using SqliteCommand recorded = connection.CreateCommand();
      recorded.Transaction = transaction;
      recorded.CommandText =
        @"INSERT OR REPLACE INTO run_state_events(run_id,state_id,seq,time_us,shape_id,value_json)
VALUES(@run,@state,@seq,@time,@shape,@value)";
      recorded.Parameters.AddWithValue("@run", runId);
      SqliteParameter eventState = recorded.Parameters.Add("@state", SqliteType.Text);
      SqliteParameter sequence = recorded.Parameters.Add("@seq", SqliteType.Integer);
      SqliteParameter time = recorded.Parameters.Add("@time", SqliteType.Integer);
      SqliteParameter eventShape = recorded.Parameters.Add("@shape", SqliteType.Text);
      SqliteParameter eventValue = recorded.Parameters.Add("@value", SqliteType.Text);
      var nextSeq = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (RunEventRecord record in captured.Events)
      {
        SaveShape(record.ShapeId, record.ShapeJson, now);
        nextSeq.TryGetValue(record.StateId, out int seq);
        nextSeq[record.StateId] = seq + 1;
        eventState.Value = record.StateId;
        sequence.Value = seq;
        time.Value = record.TimeUs;
        eventShape.Value = record.ShapeId;
        eventValue.Value = record.ValueJson;
        recorded.ExecuteNonQuery();
      }
    }
  }

  internal static StoredRunStates Load(string runId)
  {
    if (string.IsNullOrEmpty(runId))
      return StoredRunStates.Empty;
    var states = new List<StoredRunState>();
    var events = new List<StoredRunEvent>();
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using (SqliteCommand command = connection.CreateCommand())
    {
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
    }
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText =
        @"SELECT e.state_id,e.shape_id,h.shape_json,e.value_json,e.time_us
FROM run_state_events e
JOIN run_state_shapes h ON h.shape_id=e.shape_id
WHERE e.run_id=@run
ORDER BY e.time_us,e.state_id,e.seq";
      command.Parameters.AddWithValue("@run", runId);
      using SqliteDataReader reader = command.ExecuteReader();
      while (reader.Read())
        events.Add(
          new StoredRunEvent
          {
            StateId = reader.GetString(0),
            ShapeId = reader.GetString(1),
            ShapeJson = reader.GetString(2),
            ValueJson = reader.GetString(3),
            TimeUs = reader.GetInt64(4),
          }
        );
    }
    return new StoredRunStates(states, events);
  }

  /// <summary>Every shape <paramref name="stateId"/> was saved with, as a start value or a recorded value.</summary>
  internal static List<StoredRunStateShape> ListShapes(string stateId)
  {
    var shapes = new List<StoredRunStateShape>();
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"SELECT u.shape_id,h.shape_json,COUNT(DISTINCT u.run_id)
FROM (
  SELECT run_id,shape_id FROM run_states WHERE state_id=@state
  UNION ALL
  SELECT run_id,shape_id FROM run_state_events WHERE state_id=@state
) u
JOIN run_state_shapes h ON h.shape_id=u.shape_id
GROUP BY u.shape_id,h.shape_json";
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
