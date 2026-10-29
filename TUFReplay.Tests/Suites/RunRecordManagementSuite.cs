using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Management;
using TUFReplay.Activity.Migrations;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Shared.Database;
using static TestFixture;

// Run locks and whole-day deletion.
internal static class RunRecordManagementSuite
{
  internal static void RunAll(string root)
  {
    TestExistingDatabaseGainsLockColumn(root);
    TestLockedRunCannotBeDeleted(root);
    TestLockKeepsMicrophoneRecording(root);
    TestDayDeleteKeepsLockedRunsAndClears(root);
    TestDayDeleteWithoutKeepingClears(root);
    TestDayDeleteSkipsRunInUse(root);
    TestMicrophoneBatchDelete(root);
  }

  private static void TestExistingDatabaseGainsLockColumn(string root)
  {
    Open(root, "lock-upgrade.sqlite");
    SaveRun("old-run", "level-closed", 0, clear: false);
    using (SqliteConnection connection = Database.OpenConnection())
    using (SqliteCommand command = connection.CreateCommand())
    {
      // Recreate a v1 file written before the column existed.
      command.CommandText = "ALTER TABLE runs DROP COLUMN locked;";
      command.ExecuteNonQuery();
      ActivitySchema.Ensure(connection);
      command.CommandText = "SELECT locked FROM runs WHERE id='old-run'";
      Assert(Convert.ToInt32(command.ExecuteScalar()) == 0, "Existing runs were not unlocked after the upgrade.");
      command.CommandText = "PRAGMA user_version;";
      Assert(
        Convert.ToInt32(command.ExecuteScalar()) == ActivitySchema.Version,
        "The lock column changed the version."
      );
    }
    Console.WriteLine("PASS existing activity databases gain the run lock column in place");
  }

  private static void TestLockedRunCannotBeDeleted(string root)
  {
    Open(root, "lock-delete.sqlite");
    SaveRun("locked", "level-closed", 0, clear: true);
    SaveRun("free", "level-closed", 1, clear: false);

    Assert(RunLocks.Change("locked", true), "Locking an existing run failed.");
    Assert(!RunLocks.Change("missing", true), "Locking a missing run reported success.");
    Assert(RunRepository.IsLocked("locked") && !RunRepository.IsLocked("free"), "Lock state was not stored.");
    Assert(RunRepository.Get("locked").Locked, "Run reads did not report the lock.");
    Assert(!RunRepository.Delete("locked"), "A locked run was deleted.");
    Assert(RunRepository.Exists("locked"), "A locked run disappeared.");
    Assert(RunRepository.Delete("free"), "An unlocked run could not be deleted.");

    LevelSessionOverview overview = ActivityRepository.GetLevelSessionOverview("level-closed");
    Assert(
      overview.LockedRunCount == 1 && overview.LockedClearRunCount == 1,
      "Level session counts did not include the locked clear."
    );

    Assert(RunLocks.Change("locked", false), "Unlocking failed.");
    Assert(RunRepository.Delete("locked"), "An unlocked run stayed undeletable.");
    Console.WriteLine("PASS locked runs survive single deletes until unlocked");
  }

  private static void TestLockKeepsMicrophoneRecording(string root)
  {
    Open(root, "lock-microphone.sqlite");
    SaveRun("with-audio", "level-closed", 0, clear: true);
    SaveMicrophoneRecording(root, "with-audio");
    Assert(!IsMicrophonePermanent("with-audio"), "A new microphone recording started permanent.");

    RunLocks.Change("with-audio", true);
    Assert(IsMicrophonePermanent("with-audio"), "Locking did not keep the microphone recording.");

    // A recording that finishes saving after the lock is kept by the save path.
    SaveRun("late-audio", "level-closed", 1, clear: false);
    RunLocks.Change("late-audio", true);
    SaveMicrophoneRecording(root, "late-audio");
    RunLocks.KeepMicrophoneRecordingIfLocked("late-audio");
    Assert(IsMicrophonePermanent("late-audio"), "A recording saved after the lock still expires.");
    Console.WriteLine("PASS locking keeps microphone recordings, including late saves");
  }

  private static void TestDayDeleteKeepsLockedRunsAndClears(string root)
  {
    Open(root, "day-delete.sqlite");
    SaveRun("clear", "level-closed", 0, clear: true);
    SaveRun("fail", "level-closed", 1, clear: false);
    SaveRun("kept", "level-closed", 2, clear: false);
    SaveRun("other-day", "level-other", 0, clear: false);
    RunLocks.Change("kept", true);

    RunBatchDeletion result = AppSessionRunDeletion.Delete(new[] { "app-closed" }, keepClears: true, runInUseId: null);

    Assert(result.DeletedRunCount == 1, "Day delete removed the wrong number of runs.");
    Assert(result.NewlyLockedRunCount == 1, "Keep clears did not lock the clear.");
    Assert(result.KeptLockedRunCount == 2, "Kept locked runs were miscounted.");
    Assert(!RunRepository.Exists("fail"), "An unlocked failed run survived the day delete.");
    Assert(RunRepository.IsLocked("clear") && RunRepository.Exists("kept"), "Locked runs or clears were deleted.");
    Assert(RunRepository.Exists("other-day"), "Day delete touched another app session.");
    Assert(CountRows("level_sessions", "level-closed") == 1, "A session still holding locked runs was removed.");

    RunLocks.Change("clear", false);
    RunLocks.Change("kept", false);
    result = AppSessionRunDeletion.Delete(new[] { "app-closed" }, keepClears: false, runInUseId: null);
    Assert(result.DeletedRunCount == 2 && result.KeptLockedRunCount == 0, "Unlocked runs survived a second delete.");
    Assert(CountRows("level_sessions", "level-closed") == 0, "An emptied closed level session stayed.");
    Assert(CountRows("app_sessions", "app-closed") == 0, "An emptied closed app session stayed.");
    Assert(CountRows("app_sessions", "app-open") == 1, "The open app session was removed.");
    Console.WriteLine("PASS day delete keeps locked runs and clears and removes emptied closed sessions");
  }

  private static void TestDayDeleteWithoutKeepingClears(string root)
  {
    Open(root, "day-delete-clears.sqlite");
    SaveRun("clear", "level-other", 0, clear: true);
    SaveRun("fail", "level-other", 1, clear: false);

    RunBatchDeletion result = AppSessionRunDeletion.Delete(new[] { "app-open" }, keepClears: false, runInUseId: null);

    Assert(result.DeletedRunCount == 2 && result.NewlyLockedRunCount == 0, "Clears were kept without the option.");
    Assert(CountRows("level_sessions", "level-other") == 1, "The open level session was removed.");
    Assert(CountRows("app_sessions", "app-open") == 1, "The open app session was removed.");
    Console.WriteLine("PASS day delete without keep-clears deletes clears and keeps open sessions");
  }

  private static void TestDayDeleteSkipsRunInUse(string root)
  {
    Open(root, "day-delete-in-use.sqlite");
    SaveRun("playing", "level-closed", 0, clear: false);
    SaveRun("idle", "level-closed", 1, clear: false);

    RunBatchDeletion result = AppSessionRunDeletion.Delete(new[] { "app-closed" }, false, "playing");

    Assert(result.DeletedRunCount == 1 && result.KeptInUseRunCount == 1, "The replaying run was not skipped.");
    Assert(RunRepository.Exists("playing") && !RunRepository.Exists("idle"), "Day delete removed the replaying run.");
    Console.WriteLine("PASS day delete skips the run playing as a replay");
  }

  private static void TestMicrophoneBatchDelete(string root)
  {
    Open(root, "microphone-batch.sqlite");
    SaveRun("a", "level-closed", 0, clear: false);
    SaveRun("b", "level-closed", 1, clear: false);
    SaveMicrophoneRecording(root, "a");
    SaveMicrophoneRecording(root, "b");

    int deleted = MicrophoneRecordingRepository.DeleteMany(new[] { "a", "b", "missing" });

    Assert(deleted == 2, "Batch microphone delete removed the wrong number of recordings.");
    Assert(
      !MicrophoneRecordingRepository.Exists("a") && !MicrophoneRecordingRepository.Exists("b"),
      "Batch microphone delete left a recording."
    );
    Console.WriteLine("PASS microphone recordings are deleted in one batch");
  }

  // One closed app session (closed level session) and one open app session (open level session).
  private static void Open(string root, string name)
  {
    string path = Path.Combine(root, name);
    ActivityDatabaseSuite.SetDatabasePath(path);
    using SqliteConnection connection = Database.OpenConnection();
    ActivitySchema.Ensure(connection);
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"INSERT INTO app_sessions(id,started_at_utc,ended_at_utc,recorder_utc_offset_minutes)
VALUES('app-closed','2026-10-23T00:00:00Z','2026-10-23T05:00:00Z',0),
      ('app-open','2026-10-25T00:00:00Z',NULL,0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc)
VALUES('level','local:test',0,'test.adofai','2026-10-23T00:00:00Z','2026-10-25T00:00:00Z');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc,closed_at_utc)
VALUES('level-closed','level','app-closed','2026-10-23T00:00:00Z','2026-10-23T04:00:00Z'),
      ('level-other','level','app-open','2026-10-25T00:00:00Z',NULL);";
    command.ExecuteNonQuery();
  }

  private static void SaveRun(string id, string levelSessionId, int index, bool clear)
  {
    RunRepository.Save(
      new RunRecord
      {
        Id = id,
        LevelSessionId = levelSessionId,
        RunIndex = index,
        StartedAtUtc = "2026-10-23T01:00:00Z",
        EndedAtUtc = "2026-10-23T01:02:00Z",
        StartTile = 0,
        LastTile = clear ? 100 : 40,
        Result = clear ? "cleared" : "failed",
        JudgmentCounts = new JudgmentCounts(),
      }
    );
  }

  private static void SaveMicrophoneRecording(string root, string runId)
  {
    string temp = Path.Combine(root, runId + "-" + Guid.NewGuid().ToString("N") + ".wav");
    File.WriteAllBytes(temp, new byte[] { 1, 2, 3, 4 });
    MicrophoneRecordingRepository.Save(
      new CapturedMicrophoneRecording
      {
        RunId = runId,
        TempPath = temp,
        FrameCount = 2,
      }
    );
  }

  private static bool IsMicrophonePermanent(string runId)
  {
    using SqliteConnection connection = MicrophoneDatabase.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT is_permanent FROM microphone_recordings WHERE run_id=@run";
    command.Parameters.AddWithValue("@run", runId);
    return Convert.ToInt32(command.ExecuteScalar()) != 0;
  }

  private static int CountRows(string table, string id)
  {
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT count(*) FROM " + table + " WHERE id=@id";
    command.Parameters.AddWithValue("@id", id);
    return Convert.ToInt32(command.ExecuteScalar());
  }
}
