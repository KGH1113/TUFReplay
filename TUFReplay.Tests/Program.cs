using System.Reflection;
using Microsoft.Data.Sqlite;
using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;
using TUFReplay.Infrastructure.Database;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Database.Schema;

internal static class Program
{
  private static int Main()
  {
    string root = Path.Combine(Path.GetTempPath(), "tufreplay-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      NativeSqliteLoader.Initialize();
      TestWavWriter(root);
      TestSchemaMigrationAndBlob(root);
      Console.WriteLine("TUFReplay C# tests passed.");
      return 0;
    }
    catch (Exception exception)
    {
      Console.Error.WriteLine(exception);
      return 1;
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  private static void TestWavWriter(string root)
  {
    string path = Path.Combine(root, "writer.wav");
    using (var writer = new Pcm16WavWriter(path, 2))
    {
      float[] stereo = { 2f, -2f, 2f, 2f, -2f, -2f, 0.5f, 0.5f };
      Assert(writer.TryEnqueue(stereo, stereo.Length, 2), "WAV chunk was not queued.");
      Assert(writer.Complete() == 4, "WAV frame count is incorrect.");
    }

    byte[] wav = File.ReadAllBytes(path);
    Assert(System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF", "RIFF header is missing.");
    Assert(System.Text.Encoding.ASCII.GetString(wav, 8, 4) == "WAVE", "WAVE header is missing.");
    Assert(BitConverter.ToInt32(wav, 40) == 8, "PCM byte length is incorrect.");
    Assert(BitConverter.ToInt16(wav, 44) == 0, "Stereo downmix is incorrect.");
    Assert(BitConverter.ToInt16(wav, 46) == short.MaxValue, "Positive clipping is incorrect.");
    Assert(BitConverter.ToInt16(wav, 48) == short.MinValue, "Negative clipping is incorrect.");
  }

  private static void TestSchemaMigrationAndBlob(string root)
  {
    string path = Path.Combine(root, "activity.sqlite");
    SetDatabasePath(path);
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      using SqliteCommand version = connection.CreateCommand();
      version.CommandText = "PRAGMA user_version";
      Assert(Convert.ToInt32(version.ExecuteScalar()) == 8, "Fresh schema version is not 8.");
      InsertRun(connection);
    }

    string wavPath = Path.Combine(root, "blob.wav.save-pending");
    using (var writer = new Pcm16WavWriter(wavPath))
    {
      Assert(writer.TryEnqueue(new[] { 0f, 0.25f, -0.25f }, 3, 1), "BLOB WAV chunk was not queued.");
      writer.Complete();
    }
    var recording = new CapturedMicrophoneRecording
    {
      RunId = "run",
      TempPath = wavPath,
      SampleRate = 48000,
      Channels = 1,
      FrameCount = 3,
      DeviceId = "test-device",
      CaptureStartOffsetUs = 123,
    };
    MicrophoneRecordingRepository.Save(recording);

    using (SqliteConnection connection = Database.OpenConnection())
    {
      using SqliteCommand verify = connection.CreateCommand();
      verify.CommandText =
        "SELECT length(audio_wav),sample_rate,channels,frame_count,capture_start_offset_us FROM microphone_recordings WHERE run_id='run'";
      using SqliteDataReader reader = verify.ExecuteReader();
      Assert(reader.Read(), "Microphone recording row is missing.");
      Assert(reader.GetInt64(0) == new FileInfo(wavPath).Length, "Incremental BLOB length is incorrect.");
      Assert(
        reader.GetInt32(1) == 48000 && reader.GetInt32(2) == 1 && reader.GetInt64(3) == 3,
        "BLOB metadata is incorrect."
      );
      reader.Close();

      using SqliteCommand delete = connection.CreateCommand();
      delete.CommandText = "DELETE FROM runs WHERE id='run'";
      delete.ExecuteNonQuery();
      using SqliteCommand cascade = connection.CreateCommand();
      cascade.CommandText = "SELECT count(*) FROM microphone_recordings";
      Assert(Convert.ToInt32(cascade.ExecuteScalar()) == 0, "Run deletion did not cascade to microphone recording.");
    }

    string migrationPath = Path.Combine(root, "migration.sqlite");
    File.Copy(path, migrationPath);
    using var migration = new SqliteConnection("Data Source=" + migrationPath);
    migration.Open();
    using (SqliteCommand setup = migration.CreateCommand())
    {
      setup.CommandText = "DROP TABLE microphone_recordings; PRAGMA user_version=7; PRAGMA foreign_keys=ON;";
      setup.ExecuteNonQuery();
    }
    ActivitySchema.Ensure(migration);
    using SqliteCommand migrated = migration.CreateCommand();
    migrated.CommandText = "SELECT user_version FROM pragma_user_version";
    Assert(Convert.ToInt32(migrated.ExecuteScalar()) == 8, "Schema 7 to 8 migration failed.");
  }

  private static void InsertRun(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"
INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes) VALUES('app','2026-01-01',0);
INSERT INTO level_sessions(id,app_session_id,level_path,opened_at_utc) VALUES('level','app','test.adofai','2026-01-01');
INSERT INTO runs(id,level_session_id,run_index,started_at_utc,start_tile,result) VALUES('run','level',0,'2026-01-01',0,'cleared');";
    command.ExecuteNonQuery();
  }

  private static void SetDatabasePath(string path)
  {
    PropertyInfo property = typeof(Database).GetProperty("DbPath", BindingFlags.Public | BindingFlags.Static);
    property.SetValue(null, path);
  }

  private static void Assert(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
