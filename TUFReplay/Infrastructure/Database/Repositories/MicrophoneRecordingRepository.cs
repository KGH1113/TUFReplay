using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Microphone;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class MicrophoneRecordingRepository
{
  private const int BlobBufferSize = 65536;

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

  public static StoredMicrophoneRecording CopyForPlayback(
    string runId,
    string destinationPath,
    CancellationToken cancellationToken
  )
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw new ArgumentException("A run ID is required.", nameof(runId));
    if (string.IsNullOrWhiteSpace(destinationPath))
      throw new ArgumentException("A playback destination is required.", nameof(destinationPath));

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    long rowId;
    StoredMicrophoneRecording recording;
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText =
        @"SELECT rowid,format,sample_rate,channels,frame_count,device_id,capture_start_offset_us,length(audio_wav)
FROM microphone_recordings
WHERE run_id=@run
LIMIT 1";
      command.Parameters.AddWithValue("@run", runId);
      using SqliteDataReader reader = command.ExecuteReader();
      if (!reader.Read())
        return null;

      rowId = reader.GetInt64(0);
      recording = new StoredMicrophoneRecording
      {
        RunId = runId,
        FilePath = destinationPath,
        Format = reader.GetString(1),
        SampleRate = reader.GetInt32(2),
        Channels = reader.GetInt32(3),
        FrameCount = reader.GetInt64(4),
        DeviceId = reader.IsDBNull(5) ? null : reader.GetString(5),
        CaptureStartOffsetUs = reader.GetInt64(6),
        ByteLength = reader.GetInt64(7),
      };
    }

    string directory = Path.GetDirectoryName(destinationPath);
    if (!string.IsNullOrEmpty(directory))
      Directory.CreateDirectory(directory);
    string partialPath = destinationPath + ".copying";
    DeleteIfExists(partialPath);

    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      using (var blob = new SqliteBlob(connection, "microphone_recordings", "audio_wav", rowId, true))
      using (var destination = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        byte[] buffer = new byte[BlobBufferSize];
        int read;
        while ((read = blob.Read(buffer, 0, buffer.Length)) > 0)
        {
          cancellationToken.ThrowIfCancellationRequested();
          destination.Write(buffer, 0, read);
        }
        destination.Flush();
      }

      cancellationToken.ThrowIfCancellationRequested();
      if (new FileInfo(partialPath).Length != recording.ByteLength)
        throw new InvalidDataException("The copied microphone BLOB length is invalid.");
      DeleteIfExists(destinationPath);
      File.Move(partialPath, destinationPath);
      return recording;
    }
    catch
    {
      DeleteIfExists(partialPath);
      throw;
    }
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
      File.Delete(path);
  }
}
