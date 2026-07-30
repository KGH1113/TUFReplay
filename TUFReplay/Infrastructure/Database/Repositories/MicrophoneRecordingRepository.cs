using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.Microphone;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;
using AudioDatabase = TUFReplay.Infrastructure.Database.MicrophoneDatabase;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class MicrophoneRecordingRepository
{
  private const int BlobBufferSize = 65536;
  private const int MetadataBatchSize = 400;
  public static readonly TimeSpan TemporaryRetention = TimeSpan.FromDays(3);

  public static bool RunExists(string runId)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM runs WHERE id=@id)";
    command.Parameters.AddWithValue("@id", runId);
    return Convert.ToInt32(command.ExecuteScalar()) != 0;
  }

  public static bool Delete(string runId)
  {
    using SqliteConnection connection = AudioDatabase.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "DELETE FROM microphone_recordings WHERE run_id=@run";
    command.Parameters.AddWithValue("@run", runId);
    return command.ExecuteNonQuery() != 0;
  }

  public static bool Exists(string runId)
  {
    using SqliteConnection connection = AudioDatabase.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM microphone_recordings WHERE run_id=@run)";
    command.Parameters.AddWithValue("@run", runId);
    return Convert.ToInt32(command.ExecuteScalar()) != 0;
  }

  public static bool KeepPermanently(string runId)
  {
    using SqliteConnection connection = AudioDatabase.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      "UPDATE microphone_recordings SET is_permanent=1,expires_at_utc=NULL WHERE run_id=@run AND is_permanent=0";
    command.Parameters.AddWithValue("@run", runId);
    return command.ExecuteNonQuery() != 0;
  }

  public static int DeleteExpired(DateTime nowUtc)
  {
    using SqliteConnection connection = AudioDatabase.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      "DELETE FROM microphone_recordings WHERE is_permanent=0 AND expires_at_utc IS NOT NULL AND expires_at_utc<=@now";
    command.Parameters.AddWithValue("@now", nowUtc.ToUniversalTime().ToString("O"));
    return command.ExecuteNonQuery();
  }

  public static void Save(CapturedMicrophoneRecording recording, DateTime? savedAtUtc = null)
  {
    long length = new FileInfo(recording.TempPath).Length;
    string expiresAtUtc = (savedAtUtc ?? DateTime.UtcNow).ToUniversalTime().Add(TemporaryRetention).ToString("O");
    using SqliteConnection connection = AudioDatabase.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    long rowId;
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText =
        @"INSERT OR REPLACE INTO microphone_recordings(
run_id,audio_wav,format,sample_rate,channels,frame_count,device_id,capture_start_offset_us,is_permanent,expires_at_utc
) VALUES(@run,zeroblob(@length),'wav/pcm16',@rate,@channels,@frames,@device,@offset,0,@expires);
SELECT rowid FROM microphone_recordings WHERE run_id=@run;";
      command.Parameters.AddWithValue("@run", recording.RunId);
      command.Parameters.AddWithValue("@length", length);
      command.Parameters.AddWithValue("@rate", recording.SampleRate);
      command.Parameters.AddWithValue("@channels", recording.Channels);
      command.Parameters.AddWithValue("@frames", recording.FrameCount);
      command.Parameters.AddWithValue("@device", (object)recording.DeviceId ?? DBNull.Value);
      command.Parameters.AddWithValue("@offset", recording.CaptureStartOffsetUs);
      command.Parameters.AddWithValue("@expires", expiresAtUtc);
      rowId = Convert.ToInt64(command.ExecuteScalar());
    }

    using (var source = new FileStream(recording.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var blob = new SqliteBlob(connection, "microphone_recordings", "audio_wav", rowId, false))
      source.CopyTo(blob, 65536);
    transaction.Commit();
  }

  public static void PopulateMetadata(IList<RunRecord> runs)
  {
    if (runs == null || runs.Count == 0)
      return;

    using SqliteConnection connection = AudioDatabase.OpenConnection();
    for (int offset = 0; offset < runs.Count; offset += MetadataBatchSize)
      PopulateMetadataBatch(connection, runs, offset, Math.Min(MetadataBatchSize, runs.Count - offset));
  }

  private static void PopulateMetadataBatch(
    SqliteConnection connection,
    IList<RunRecord> runs,
    int offset,
    int count
  )
  {
    using SqliteCommand command = connection.CreateCommand();
    var parameters = new List<string>(count);
    var byId = new Dictionary<string, RunRecord>(count, StringComparer.Ordinal);
    for (int index = 0; index < count; index++)
    {
      RunRecord run = runs[offset + index];
      if (run == null || string.IsNullOrEmpty(run.Id))
        continue;
      string parameter = "@run" + index;
      parameters.Add(parameter);
      command.Parameters.AddWithValue(parameter, run.Id);
      byId[run.Id] = run;
    }
    if (parameters.Count == 0)
      return;

    command.CommandText =
      @"SELECT run_id,length(audio_wav),sample_rate,channels,frame_count,is_permanent,expires_at_utc
FROM microphone_recordings
WHERE run_id IN ("
      + string.Join(",", parameters)
      + ")";
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      if (!byId.TryGetValue(reader.GetString(0), out RunRecord run))
        continue;
      run.MicrophoneRecordingBytes = reader.GetInt64(1);
      run.MicrophoneSampleRate = reader.GetInt32(2);
      run.MicrophoneChannels = reader.GetInt32(3);
      run.MicrophoneFrameCount = reader.GetInt64(4);
      run.MicrophoneRecordingPermanent = reader.GetInt32(5) != 0;
      run.MicrophoneRecordingExpiresAtUtc = reader.IsDBNull(6) ? null : reader.GetString(6);
    }
  }

  public static int MigrateLegacyRecordings()
  {
    int migrated = 0;
    while (TryReadLegacyRecording(out LegacyRecording legacy))
    {
      using SqliteConnection sourceConnection = DatabaseStore.OpenConnection();
      using SqliteConnection destinationConnection = AudioDatabase.OpenConnection();
      using SqliteTransaction destinationTransaction = destinationConnection.BeginTransaction();
      long destinationRowId;
      using (SqliteCommand insert = destinationConnection.CreateCommand())
      {
        insert.Transaction = destinationTransaction;
        insert.CommandText =
          @"INSERT OR REPLACE INTO microphone_recordings(
run_id,audio_wav,format,sample_rate,channels,frame_count,device_id,capture_start_offset_us,is_permanent,expires_at_utc
) VALUES(@run,zeroblob(@length),@format,@rate,@channels,@frames,@device,@offset,@permanent,@expires);
SELECT rowid FROM microphone_recordings WHERE run_id=@run;";
        insert.Parameters.AddWithValue("@run", legacy.RunId);
        insert.Parameters.AddWithValue("@length", legacy.ByteLength);
        insert.Parameters.AddWithValue("@format", legacy.Format);
        insert.Parameters.AddWithValue("@rate", legacy.SampleRate);
        insert.Parameters.AddWithValue("@channels", legacy.Channels);
        insert.Parameters.AddWithValue("@frames", legacy.FrameCount);
        insert.Parameters.AddWithValue("@device", (object)legacy.DeviceId ?? DBNull.Value);
        insert.Parameters.AddWithValue("@offset", legacy.CaptureStartOffsetUs);
        insert.Parameters.AddWithValue("@permanent", legacy.IsPermanent ? 1 : 0);
        insert.Parameters.AddWithValue("@expires", (object)legacy.ExpiresAtUtc ?? DBNull.Value);
        destinationRowId = Convert.ToInt64(insert.ExecuteScalar());
      }

      long copied;
      using (var source = new SqliteBlob(sourceConnection, "microphone_recordings", "audio_wav", legacy.RowId, true))
      using (var destination = new SqliteBlob(
        destinationConnection,
        "microphone_recordings",
        "audio_wav",
        destinationRowId,
        false
      ))
        copied = CopyBlob(source, destination);
      if (copied != legacy.ByteLength)
        throw new InvalidDataException("The migrated microphone BLOB length is invalid.");
      destinationTransaction.Commit();

      using SqliteCommand delete = sourceConnection.CreateCommand();
      delete.CommandText = "DELETE FROM microphone_recordings WHERE rowid=@rowid AND run_id=@run";
      delete.Parameters.AddWithValue("@rowid", legacy.RowId);
      delete.Parameters.AddWithValue("@run", legacy.RunId);
      delete.ExecuteNonQuery();
      migrated++;
    }
    return migrated;
  }

  public static void ReclaimLegacyStorage()
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "VACUUM";
    command.ExecuteNonQuery();
  }

  public static int DeleteOrphans()
  {
    using SqliteConnection connection = AudioDatabase.OpenConnection();
    using (SqliteCommand attach = connection.CreateCommand())
    {
      attach.CommandText = "ATTACH DATABASE @path AS activity";
      attach.Parameters.AddWithValue("@path", DatabaseStore.DbPath);
      attach.ExecuteNonQuery();
    }
    try
    {
      using SqliteCommand delete = connection.CreateCommand();
      delete.CommandText =
        @"DELETE FROM microphone_recordings
WHERE NOT EXISTS(SELECT 1 FROM activity.runs WHERE activity.runs.id=microphone_recordings.run_id)";
      return delete.ExecuteNonQuery();
    }
    finally
    {
      using SqliteCommand detach = connection.CreateCommand();
      detach.CommandText = "DETACH DATABASE activity";
      detach.ExecuteNonQuery();
    }
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

    using SqliteConnection connection = AudioDatabase.OpenConnection();
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

  public static StoredMicrophoneRecording WriteTo(
    string runId,
    Stream destination,
    CancellationToken cancellationToken
  )
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw new ArgumentException("A run ID is required.", nameof(runId));
    if (destination == null)
      throw new ArgumentNullException(nameof(destination));
    if (!destination.CanWrite)
      throw new ArgumentException("The microphone destination must be writable.", nameof(destination));

    using SqliteConnection connection = AudioDatabase.OpenConnection();
    long rowId;
    StoredMicrophoneRecording recording;
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText =
        @"SELECT rowid,format,sample_rate,channels,frame_count,capture_start_offset_us,length(audio_wav)
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
        Format = reader.GetString(1),
        SampleRate = reader.GetInt32(2),
        Channels = reader.GetInt32(3),
        FrameCount = reader.GetInt64(4),
        CaptureStartOffsetUs = reader.GetInt64(5),
        ByteLength = reader.GetInt64(6),
      };
    }

    cancellationToken.ThrowIfCancellationRequested();
    long written = 0;
    using (var blob = new SqliteBlob(connection, "microphone_recordings", "audio_wav", rowId, true))
    {
      byte[] buffer = new byte[BlobBufferSize];
      int read;
      while ((read = blob.Read(buffer, 0, buffer.Length)) > 0)
      {
        cancellationToken.ThrowIfCancellationRequested();
        destination.Write(buffer, 0, read);
        written += read;
      }
    }

    if (written != recording.ByteLength)
      throw new InvalidDataException("The copied microphone BLOB length is invalid.");
    return recording;
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
      File.Delete(path);
  }

  private static bool TryReadLegacyRecording(out LegacyRecording recording)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using (SqliteCommand exists = connection.CreateCommand())
    {
      exists.CommandText =
        "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name='microphone_recordings')";
      if (Convert.ToInt32(exists.ExecuteScalar()) == 0)
      {
        recording = null;
        return false;
      }
    }

    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"SELECT rowid,run_id,format,sample_rate,channels,frame_count,device_id,capture_start_offset_us,
is_permanent,expires_at_utc,length(audio_wav)
FROM microphone_recordings
ORDER BY rowid
LIMIT 1";
    using SqliteDataReader reader = command.ExecuteReader();
    if (!reader.Read())
    {
      recording = null;
      return false;
    }
    recording = new LegacyRecording
    {
      RowId = reader.GetInt64(0),
      RunId = reader.GetString(1),
      Format = reader.GetString(2),
      SampleRate = reader.GetInt32(3),
      Channels = reader.GetInt32(4),
      FrameCount = reader.GetInt64(5),
      DeviceId = reader.IsDBNull(6) ? null : reader.GetString(6),
      CaptureStartOffsetUs = reader.GetInt64(7),
      IsPermanent = reader.GetInt32(8) != 0,
      ExpiresAtUtc = reader.IsDBNull(9) ? null : reader.GetString(9),
      ByteLength = reader.GetInt64(10),
    };
    return true;
  }

  private static long CopyBlob(Stream source, Stream destination)
  {
    byte[] buffer = new byte[BlobBufferSize];
    int read;
    long copied = 0;
    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
    {
      destination.Write(buffer, 0, read);
      copied += read;
    }
    return copied;
  }

  private sealed class LegacyRecording
  {
    public long RowId;
    public string RunId;
    public string Format;
    public int SampleRate;
    public int Channels;
    public long FrameCount;
    public string DeviceId;
    public long CaptureStartOffsetUs;
    public bool IsPermanent;
    public string ExpiresAtUtc;
    public long ByteLength;
  }
}
