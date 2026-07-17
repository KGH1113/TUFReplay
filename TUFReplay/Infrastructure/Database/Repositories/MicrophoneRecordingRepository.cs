using System;
using System.IO;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Microphone;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class MicrophoneRecordingRepository
{
  public static bool RunExists(string runId)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM runs WHERE id=@id)";
    command.Parameters.AddWithValue("@id", runId);
    return Convert.ToInt32(command.ExecuteScalar()) != 0;
  }

  public static void Save(CapturedMicrophoneRecording recording)
  {
    long length = new FileInfo(recording.TempPath).Length;
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    long rowId;
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText =
        @"INSERT OR REPLACE INTO microphone_recordings(
run_id,audio_wav,format,sample_rate,channels,frame_count,device_id,capture_start_offset_us
) VALUES(@run,zeroblob(@length),'wav/pcm16',@rate,@channels,@frames,@device,@offset);
SELECT rowid FROM microphone_recordings WHERE run_id=@run;";
      command.Parameters.AddWithValue("@run", recording.RunId);
      command.Parameters.AddWithValue("@length", length);
      command.Parameters.AddWithValue("@rate", recording.SampleRate);
      command.Parameters.AddWithValue("@channels", recording.Channels);
      command.Parameters.AddWithValue("@frames", recording.FrameCount);
      command.Parameters.AddWithValue("@device", (object)recording.DeviceId ?? DBNull.Value);
      command.Parameters.AddWithValue("@offset", recording.CaptureStartOffsetUs);
      rowId = Convert.ToInt64(command.ExecuteScalar());
    }

    using (var source = new FileStream(recording.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var blob = new SqliteBlob(connection, "microphone_recordings", "audio_wav", rowId, false))
      source.CopyTo(blob, 65536);
    transaction.Commit();
  }
}
