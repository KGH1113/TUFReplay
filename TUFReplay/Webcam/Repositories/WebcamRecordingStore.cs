using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TUFReplay.Shared.Threading;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Repositories;

// Video stays on disk. No BLOB copies, full-file reads or per-frame database writes.
public sealed class WebcamRecordingStore
{
  private readonly object _gate = new object();
  private readonly string _directory;
  private readonly HashSet<string> _deleted = new HashSet<string>(StringComparer.Ordinal);
  private readonly HashSet<string> _deletedKeys = new HashSet<string>(StringComparer.Ordinal);
  private readonly Dictionary<string, int> _leases = new Dictionary<string, int>(StringComparer.Ordinal);
  private readonly SerialBackgroundQueue _releases = new SerialBackgroundQueue(exception =>
    Main.Instance?.LogException("Camera/Release", exception)
  );
  internal Task PendingReleases => _releases.Completion;

  public WebcamRecordingStore(string directory)
  {
    _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    Directory.CreateDirectory(directory);
  }

  public string TemporaryPath(string runId) => Path.Combine(_directory, Key(runId) + ".mp4.partial");

  public long ReserveCaptureBytes(long budgetBytes, int retentionDays)
  {
    lock (_gate)
    {
      long reservation = Math.Min(128L * 1024 * 1024, budgetBytes);
      long stored = PruneLocked(budgetBytes - reservation, retentionDays, DateTime.UtcNow);
      return Math.Max(0L, Math.Min(reservation, budgetBytes - stored));
    }
  }

  public bool Save(WebcamRecording recording, long budgetBytes, int retentionDays)
  {
    if (recording == null)
      return false;
    lock (_gate)
    {
      if (_deleted.Contains(recording.RunId))
      {
        DeleteFile(recording.FilePath);
        return false;
      }
      string key = Key(recording.RunId);
      string videoPath = Path.Combine(_directory, key + ".mp4");
      string metadataPath = Path.Combine(_directory, key + ".json");
      if (File.Exists(videoPath) || File.Exists(metadataPath))
      {
        if (recording.FilePath != videoPath)
          DeleteFile(recording.FilePath);
        return false;
      }
      Validate(recording);
      long bytes = new FileInfo(recording.FilePath).Length;
      long stored = PruneLocked(budgetBytes - bytes, retentionDays, DateTime.UtcNow);
      if (bytes > Math.Min(128L * 1024 * 1024, budgetBytes) || stored + bytes > budgetBytes)
      {
        DeleteFile(recording.FilePath);
        return false;
      }
      string pending = metadataPath + ".partial";
      bool moved = false;
      try
      {
        File.WriteAllText(pending, JsonConvert.SerializeObject(recording));
        File.Move(recording.FilePath, videoPath);
        moved = true;
        File.Move(pending, metadataPath);
      }
      catch
      {
        DeleteFile(pending);
        if (moved)
          DeleteFile(videoPath);
        throw;
      }
      recording.FilePath = videoPath;
      return true;
    }
  }

  public WebcamRecordingLease Acquire(string runId)
  {
    lock (_gate)
    {
      string key = Key(runId);
      string metadataPath = Path.Combine(_directory, key + ".json");
      if (_deleted.Contains(runId) || !File.Exists(metadataPath))
        return null;
      if (new FileInfo(metadataPath).Length > 4 * 1024 * 1024)
        throw new InvalidDataException("Camera recording metadata is too large.");
      var recording = JsonConvert.DeserializeObject<WebcamRecording>(File.ReadAllText(metadataPath));
      if (recording == null || recording.RunId != runId)
        throw new InvalidDataException("Camera recording metadata does not match this replay.");
      recording.FilePath = Path.Combine(_directory, key + ".mp4");
      Validate(recording);
      _leases.TryGetValue(key, out int count);
      _leases[key] = count + 1;
      return new WebcamRecordingLease(recording, () => Release(key));
    }
  }

  public void DeleteRun(string runId)
  {
    lock (_gate)
    {
      _deleted.Add(runId);
      string key = Key(runId);
      _deletedKeys.Add(key);
      DeleteFile(TemporaryPath(runId));
      if (!_leases.ContainsKey(key))
        DeleteStored(key);
    }
  }

  public void Cleanup(long budgetBytes, int retentionDays)
  {
    lock (_gate)
      PruneLocked(budgetBytes, retentionDays, DateTime.UtcNow);
  }

  // Called once before any capture starts; interrupted MP4s cannot be replayed.
  public void CleanupInterruptedCaptures()
  {
    lock (_gate)
    {
      foreach (string path in Directory.EnumerateFiles(_directory, "*.partial"))
        DeleteFile(path);
      foreach (string path in Directory.EnumerateFiles(_directory, "*.mp4"))
        if (!File.Exists(Path.ChangeExtension(path, ".json")))
          DeleteFile(path);
      foreach (string path in Directory.EnumerateFiles(_directory, "*.json"))
        if (!File.Exists(Path.ChangeExtension(path, ".mp4")))
          DeleteFile(path);
    }
  }

  public static void Discard(WebcamRecording recording)
  {
    if (recording != null)
      DeleteFile(recording.FilePath);
  }

  private long PruneLocked(long budgetBytes, int retentionDays, DateTime now)
  {
    var files = Directory
      .EnumerateFiles(_directory, "*.mp4")
      .Select(path => new FileInfo(path))
      .OrderBy(file => file.LastWriteTimeUtc)
      .ToList();
    long total = files.Sum(file => file.Length);
    DateTime expiresBefore = now.AddDays(-retentionDays);
    foreach (FileInfo file in files)
    {
      if (total <= budgetBytes && file.LastWriteTimeUtc >= expiresBefore)
        continue;
      string key = Path.GetFileNameWithoutExtension(file.Name);
      if (_leases.ContainsKey(key))
        continue;
      long length = file.Length;
      if (DeleteStored(key))
        total -= length;
    }
    return total;
  }

  private bool DeleteStored(string key)
  {
    string video = Path.Combine(_directory, key + ".mp4");
    if (!DeleteFile(video))
      return false;
    DeleteFile(Path.Combine(_directory, key + ".json"));
    return true;
  }

  private void Release(string key)
  {
    // Unity's VideoPlayer disposal must never wait for retention scans or disk
    // deletes holding the storage gate. The lease stays pinned until this runs.
    _releases.Enqueue(() => ReleaseLocked(key));
  }

  private void ReleaseLocked(string key)
  {
    lock (_gate)
    {
      if (_leases[key] > 1)
        _leases[key]--;
      else
      {
        _leases.Remove(key);
        if (_deletedKeys.Contains(key))
          DeleteStored(key);
      }
    }
  }

  private static void Validate(WebcamRecording recording)
  {
    if (
      recording.SchemaVersion != 1
      || recording.DurationUs <= 0
      || recording.Width <= 0
      || recording.Height <= 0
      || recording.FrameRate <= 0
      || recording.GameplayRate <= 0
      || double.IsNaN(recording.GameplayRate)
      || double.IsInfinity(recording.GameplayRate)
    )
      throw new InvalidDataException("Camera recording metadata is invalid.");
    WebcamTimelineSegment previous = null;
    foreach (WebcamTimelineSegment segment in recording.Timeline ?? Array.Empty<WebcamTimelineSegment>())
    {
      if (
        segment == null
        || segment.GameplayRate <= 0
        || double.IsNaN(segment.GameplayRate)
        || double.IsInfinity(segment.GameplayRate)
        || (
          previous != null
          && (segment.TimelineTimeUs <= previous.TimelineTimeUs || segment.VideoTimeUs < previous.VideoTimeUs)
        )
      )
        throw new InvalidDataException("Camera synchronization metadata is invalid.");
      previous = segment;
    }
    using var stream = File.OpenRead(recording.FilePath);
    var header = new byte[8];
    if (stream.Length < 32 || stream.Read(header, 0, 8) != 8 || Encoding.ASCII.GetString(header, 4, 4) != "ftyp")
      throw new InvalidDataException("The camera video is incomplete.");
  }

  private static string Key(string runId)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw new ArgumentException("A run id is required.", nameof(runId));
    using var hash = SHA256.Create();
    return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(runId))).Replace("-", "").ToLowerInvariant();
  }

  private static bool DeleteFile(string path)
  {
    try
    {
      if (!string.IsNullOrEmpty(path))
        File.Delete(path);
      return true;
    }
    catch (IOException)
    {
      return false;
    }
    catch (UnauthorizedAccessException)
    {
      return false;
    }
  }
}

public sealed class WebcamRecordingLease : IDisposable
{
  private Action _release;

  internal WebcamRecordingLease(WebcamRecording recording, Action release)
  {
    Recording = recording;
    _release = release;
  }

  public WebcamRecording Recording { get; }

  public void Dispose() => System.Threading.Interlocked.Exchange(ref _release, null)?.Invoke();
}
