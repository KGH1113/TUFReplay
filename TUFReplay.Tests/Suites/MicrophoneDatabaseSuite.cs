using Microsoft.Data.Sqlite;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Shared.Database;
using static TestFixture;

// A deleted recording frees its space instead of leaving the file at its largest size.
internal static class MicrophoneDatabaseSuite
{
  private const int RecordingBytes = 256 * 1024;
  private const int PageBytes = 4096;

  internal static void RunAll(string root)
  {
    TestNewDatabaseShrinksOnDelete(root);
    TestOlderDatabaseIsRebuiltOnce(root);
  }

  private static void TestNewDatabaseShrinksOnDelete(string root)
  {
    MicrophoneDatabase.Initialize(Path.Combine(root, "new.microphones.sqlite"));
    Assert(ReadPragma("auto_vacuum") == 1, "A new microphone database was created without auto-vacuum.");
    SaveRecording(root, "first");
    SaveRecording(root, "second");
    long pages = ReadPragma("page_count");

    Assert(MicrophoneRecordingRepository.Delete("first"), "The microphone recording was not deleted.");

    Assert(ReadPragma("freelist_count") == 0, "Deleting a microphone recording left free pages in the file.");
    Assert(
      pages - ReadPragma("page_count") >= RecordingBytes / PageBytes,
      "Deleting a microphone recording did not shrink the file."
    );
    Console.WriteLine("PASS deleting a microphone recording shrinks its database");
  }

  private static void TestOlderDatabaseIsRebuiltOnce(string root)
  {
    string path = Path.Combine(root, "older.microphones.sqlite");
    CreateOlderDatabase(path);
    MicrophoneDatabase.Initialize(path);
    SaveRecording(root, "kept");
    SaveRecording(root, "deleted");
    MicrophoneRecordingRepository.Delete("deleted");
    Assert(
      ReadPragma("auto_vacuum") == 0 && ReadPragma("freelist_count") > 0,
      "The older database fixture already frees deleted space."
    );

    Assert(MicrophoneDatabase.EnableAutoVacuum(), "An older microphone database was not rebuilt.");

    Assert(ReadPragma("auto_vacuum") == 1, "The rebuilt microphone database still lacks auto-vacuum.");
    Assert(ReadPragma("freelist_count") == 0, "The rebuild left free pages in the file.");
    Assert(
      MicrophoneRecordingRepository.GetByteLength("kept") == RecordingBytes,
      "The rebuild changed a kept recording."
    );
    Assert(!MicrophoneDatabase.EnableAutoVacuum(), "A converted microphone database was rebuilt again.");
    Console.WriteLine("PASS an older microphone database is rebuilt once to free deleted space");
  }

  // The table as an earlier build created it, without auto-vacuum.
  private static void CreateOlderDatabase(string path)
  {
    using var connection = new SqliteConnection("Data Source=" + path + ";Pooling=False");
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"PRAGMA auto_vacuum=NONE;
PRAGMA journal_mode=WAL;
CREATE TABLE microphone_recordings (
  run_id TEXT PRIMARY KEY,
  audio_wav BLOB NOT NULL,
  format TEXT NOT NULL,
  sample_rate INTEGER NOT NULL,
  channels INTEGER NOT NULL,
  frame_count INTEGER NOT NULL,
  device_id TEXT,
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0,
  is_permanent INTEGER NOT NULL DEFAULT 0,
  expires_at_utc TEXT
);
PRAGMA user_version = 1;";
    command.ExecuteNonQuery();
  }

  private static void SaveRecording(string root, string runId)
  {
    string temp = Path.Combine(root, runId + "-" + Guid.NewGuid().ToString("N") + ".wav");
    File.WriteAllBytes(temp, new byte[RecordingBytes]);
    MicrophoneRecordingRepository.Save(
      new CapturedMicrophoneRecording
      {
        RunId = runId,
        TempPath = temp,
        FrameCount = RecordingBytes / 2,
      }
    );
  }

  private static long ReadPragma(string name)
  {
    using SqliteConnection connection = MicrophoneDatabase.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA " + name;
    return Convert.ToInt64(command.ExecuteScalar());
  }
}
