using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Repositories;
using TUFReplay.Composition;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Preparation;
using TUFReplay.Shared.Unity;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Playback;
using TUFReplay.Webcam.Repositories;
using UnityEngine;

namespace TUFReplay.Replay.Export;

// This module owns only neutral files. No renderer assembly is loaded or referenced.
public static class RenderBundleExportService
{
  private static readonly object Gate = new object();
  private static readonly Dictionary<string, Job> Jobs = new Dictionary<string, Job>();

  public static object Start(string runId, string levelPath, bool webcam, bool microphone)
  {
    lock (Gate)
    {
      if (Jobs.Values.Any(job => !job.Finished))
        return Error("render_export_busy", "Another recording is being prepared. Wait or cancel it first.");
      if (ReplayPlaybackCoordinator.IsBusy)
        return Error("replay_busy", "Stop replay playback before preparing a render.");
      var settings = JsonConvert.DeserializeObject<TUFReplaySetting>(JsonConvert.SerializeObject(Main.Settings));
      settings.Normalize();
      var job = new Job
      {
        Id = Guid.NewGuid().ToString("N"),
        RunId = runId,
        LevelPath = levelPath,
        Settings = settings,
        IncludeWebcam = webcam,
        IncludeMicrophone = microphone,
        ScreenWidth = Math.Max(1, Screen.width),
        ScreenHeight = Math.Max(1, Screen.height),
      };
      job.Directory = Path.Combine(Main.Instance.Path, "RenderBundles", job.Id);
      Jobs.Add(job.Id, job);
      Task.Run(() => Export(job));
      return StatusLocked(job);
    }
  }

  public static object GetStatus(string jobId)
  {
    lock (Gate)
      return Jobs.TryGetValue(jobId, out Job job)
        ? StatusLocked(job)
        : Error("render_export_not_found", "This preparation job has expired. Start rendering again.");
  }

  public static object Cancel(string jobId)
  {
    lock (Gate)
    {
      if (!Jobs.TryGetValue(jobId, out Job job))
        return Error("render_export_not_found", "This preparation job has expired. Start rendering again.");
      if (!job.Finished)
        job.Cancellation.Cancel();
      return StatusLocked(job);
    }
  }

  public static void Shutdown()
  {
    lock (Gate)
      foreach (Job job in Jobs.Values)
        if (!job.Finished)
          job.Cancellation.Cancel();
  }

  private static async Task Export(Job job)
  {
    CancellationToken token = job.Cancellation.Token;
    try
    {
      CleanupExpired(job);
      token.ThrowIfCancellationRequested();
      StoredReplayRun run = RunRepository.GetReplayRun(job.RunId);
      if (run == null)
        throw new ExportException("run_not_found", "This play record no longer exists. Refresh the activity list.");
      if (
        run.EngineId != ReplayFormat.EngineId
        || run.FormatVersion != ReplayFormat.FormatVersion
        || !string.IsNullOrEmpty(run.ReplayUnavailableReason)
      )
        throw new ExportException(
          "render_recording_incompatible",
          "This recording cannot be rendered. Record a new run with the current TUFReplay version."
        );
      if (run.StartTile != 0)
        throw new ExportException(
          "render_start_tile_unsupported",
          "Rendering currently supports recordings that start at the first tile. Record a new run from the beginning of the level."
        );
      var meta = JsonConvert.DeserializeObject<ReplayMetadata>(run.MetaJson ?? "{}");
      ValidateMetadata(meta);
      string target = job.LevelPath ?? run.LevelPath;
      if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
        throw new ExportException(
          "level_file_missing",
          "The recorded level file is missing. Restore it or choose a matching level file."
        );
      string fileHash = FileHash(target);
      await ValidateLevel(job, run, target, token);
      if (fileHash != FileHash(job.LevelPath))
        throw new ExportException(
          "level_gameplay_modified",
          "The level changed while preparing this recording. Try again after saving the level."
        );
      token.ThrowIfCancellationRequested();
      var inputs = ReplayInputParser.Parse(run.InputCsv);
      var hits = ReplayHitContextParser.Parse(run.HitContextCsv);
      Directory.CreateDirectory(job.Directory);
      using (var writer = new StreamWriter(Path.Combine(job.Directory, "inputs.csv"), false, new UTF8Encoding(false)))
        RenderBundleCsv.WriteInputs(writer, inputs, meta.inputNativePlatform, meta.terminalTimeUs.Value, token);
      using (var writer = new StreamWriter(Path.Combine(job.Directory, "hits.csv"), false, new UTF8Encoding(false)))
        RenderBundleCsv.WriteHits(
          writer,
          hits,
          meta.terminalTimeUs.Value,
          value => Enum.GetName(typeof(HitMargin), value),
          token
        );
      SetProgress(job, 0.35);
      var media = new JObject();
      if (job.IncludeWebcam)
      {
        using WebcamRecordingLease lease = FeatureRegistry.WebcamRecording?.Store?.Acquire(run.Id);
        if (lease != null)
        {
          WebcamRecording video = lease.Recording;
          await CopyFile(video.FilePath, Path.Combine(job.Directory, "webcam.mp4"), token);
          WebcamCropRect crop = WebcamCropRect.Get(job.Settings);
          WebcamOverlayBounds layout = WebcamOverlayLayout.Get(
            job.Settings,
            job.ScreenWidth,
            job.ScreenHeight,
            crop.AspectRatio((double)video.Width / video.Height)
          );
          media["webcam"] = JObject.FromObject(
            new
            {
              path = "webcam.mp4",
              width = video.Width,
              height = video.Height,
              durationUs = video.DurationUs,
              captureStartOffsetUs = video.CaptureStartOffsetUs,
              gameplayRate = video.GameplayRate,
              timeline = (video.Timeline ?? Array.Empty<WebcamTimelineSegment>()).Select(segment => new
              {
                timelineTimeUs = segment.TimelineTimeUs,
                videoTimeUs = segment.VideoTimeUs,
                gameplayRate = segment.GameplayRate,
              }),
              offsetMs = job.Settings.WebcamOffsetMs,
              mirror = job.Settings.WebcamMirror,
              crop = new
              {
                left = crop.X,
                top = crop.Y,
                right = crop.X + crop.Width,
                bottom = crop.Y + crop.Height,
              },
              layout = new
              {
                left = layout.X / job.ScreenWidth,
                top = layout.Y / job.ScreenHeight,
                width = layout.Width / job.ScreenWidth,
                height = layout.Height / job.ScreenHeight,
              },
            }
          );
        }
      }
      SetProgress(job, 0.7);
      if (job.IncludeMicrophone)
      {
        var microphone = MicrophoneRecordingRepository.CopyForPlayback(
          run.Id,
          Path.Combine(job.Directory, "microphone.wav"),
          token
        );
        if (microphone != null)
          media["microphone"] = JObject.FromObject(
            new
            {
              path = "microphone.wav",
              sampleRate = microphone.SampleRate,
              channels = microphone.Channels,
              frameCount = microphone.FrameCount,
              captureStartOffsetUs = microphone.CaptureStartOffsetUs,
              latencyUs = (long)job.Settings.MicrophoneOffsetMs * 1000,
              volume = Math.Pow(10, job.Settings.MicrophoneVolumeDb / 20d),
            }
          );
      }
      token.ThrowIfCancellationRequested();
      var manifest = JObject.FromObject(
        new
        {
          schemaVersion = 1,
          recordingId = run.Id,
          level = new
          {
            path = job.LevelPath,
            fileSha256 = fileHash,
            gameplayHash = BitConverter.ToString(run.GameplayHash).Replace("-", "").ToLowerInvariant(),
            gameplayHashVersion = run.GameplayHashVersion,
          },
          replay = new
          {
            gameplayStartSongPosition = meta.gameplayStartSongPosition.Value,
            effectivePitch = (double)meta.effectivePitch.Value,
            gameInputOffsetMs = meta.gameInputOffsetMs.Value,
            noFailMode = meta.noFailMode ?? run.NoFailMode,
            judgmentSystem = meta.judgmentSystem,
            judgmentDifficulty = run.JudgmentDifficulty?.ToString(),
            startTile = run.StartTile,
            wonTimeUs = meta.wonTimeUs,
            terminalTimeUs = meta.terminalTimeUs.Value,
          },
          inputsFile = "inputs.csv",
          hitsFile = "hits.csv",
        }
      );
      manifest["media"] = media;
      string manifestPath = Path.Combine(job.Directory, "manifest.json");
      File.WriteAllText(manifestPath, manifest.ToString(Formatting.Indented), new UTF8Encoding(false));
      lock (Gate)
      {
        token.ThrowIfCancellationRequested();
        job.ManifestPath = manifestPath;
        job.State = "completed";
        job.Progress = 1;
      }
    }
    catch (OperationCanceledException)
    {
      DeleteDirectory(job.Directory);
      lock (Gate)
        job.State = "cancelled";
    }
    catch (Exception exception)
    {
      DeleteDirectory(job.Directory);
      Main.Instance?.LogException("Render/Export", exception);
      lock (Gate)
      {
        job.State = "failed";
        job.ErrorCode =
          (exception as ExportException)?.Code
          ?? (exception is InvalidDataException ? "render_recording_incompatible" : "render_export_failed");
        job.ErrorMessage =
          exception is ExportException ? exception.Message
          : exception is InvalidDataException
            ? "This recording has incomplete or unsupported replay data. Record a new run with the current TUFReplay version."
          : "The recording could not be prepared. Check the level file and available disk space, then try again.";
      }
    }
  }

  private static async Task ValidateLevel(Job job, StoredReplayRun run, string target, CancellationToken token)
  {
    var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var registration = token.Register(() => result.TrySetCanceled());
    UnityMainThread.Post(() =>
    {
      if (token.IsCancellationRequested)
        return;
      try
      {
        if (
          !ReplayLevelHashValidator.ValidateTarget(
            run,
            target,
            out string canonical,
            out string code,
            out string message
          )
        )
          throw new ExportException(code, message);
        if (!File.Exists(canonical))
          throw new ExportException(
            "level_file_missing",
            "The recorded level file is missing. Restore it or choose a matching level file."
          );
        if (!GameplayChartHash.TryLoadCustomLevel(canonical, out var data, out _, out message))
          throw new ExportException("level_file_invalid", message);
        if (!ReplayLevelHashValidator.ValidateLoaded(run, data, canonical, out code, out message))
          throw new ExportException(code, message);
        job.LevelPath = canonical;
        result.TrySetResult(true);
      }
      catch (Exception exception)
      {
        result.TrySetException(exception);
      }
    });
    await result.Task;
  }

  private static void ValidateMetadata(ReplayMetadata meta)
  {
    if (
      meta == null
      || !meta.gameplayStartSongPosition.HasValue
      || !Finite(meta.gameplayStartSongPosition.Value)
      || !meta.effectivePitch.HasValue
      || !Finite(meta.effectivePitch.Value)
      || meta.effectivePitch <= 0
      || !meta.gameInputOffsetMs.HasValue
      || !meta.terminalTimeUs.HasValue
      || meta.terminalTimeUs < 0
      || (meta.wonTimeUs.HasValue && (meta.wonTimeUs < 0 || meta.wonTimeUs > meta.terminalTimeUs))
      || string.IsNullOrWhiteSpace(meta.judgmentSystem)
    )
      throw new ExportException(
        "render_recording_incompatible",
        "This recording lacks timing or judgment data. Record a new run with the current TUFReplay version."
      );
  }

  private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

  private static string FileHash(string path)
  {
    using var stream = File.OpenRead(path);
    using var hash = SHA256.Create();
    return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
  }

  private static async Task CopyFile(string source, string destination, CancellationToken token)
  {
    using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
    using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
    await input.CopyToAsync(output, 131072, token);
  }

  private static void SetProgress(Job job, double progress)
  {
    lock (Gate)
      job.Progress = progress;
  }

  private static object StatusLocked(Job job) =>
    new
    {
      jobId = job.Id,
      runId = job.RunId,
      state = job.State,
      progress = job.Progress,
      manifestPath = job.ManifestPath,
      errorCode = job.ErrorCode,
      errorMessage = job.ErrorMessage,
    };

  private static object Error(string code, string message) => new { error = new { code, message } };

  private static void CleanupExpired(Job current)
  {
    string root = Path.GetDirectoryName(current.Directory);
    if (!Directory.Exists(root))
      return;
    foreach (string directory in Directory.EnumerateDirectories(root))
      if (Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddDays(-1))
        DeleteDirectory(directory);
    lock (Gate)
      foreach (
        string id in Jobs.Where(pair => pair.Value.Finished && pair.Value.CreatedAt < DateTime.UtcNow.AddDays(-1))
          .Select(pair => pair.Key)
          .ToArray()
      )
      {
        Jobs[id].Cancellation.Dispose();
        Jobs.Remove(id);
      }
  }

  private static void DeleteDirectory(string directory)
  {
    try
    {
      if (Directory.Exists(directory))
        Directory.Delete(directory, true);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  private sealed class Job
  {
    public string Id,
      RunId,
      LevelPath,
      Directory,
      ManifestPath,
      ErrorCode,
      ErrorMessage;
    public string State = "preparing";
    public double Progress;
    public bool IncludeWebcam,
      IncludeMicrophone;
    public int ScreenWidth,
      ScreenHeight;
    public TUFReplaySetting Settings;
    public readonly DateTime CreatedAt = DateTime.UtcNow;
    public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
    public bool Finished => State == "completed" || State == "failed" || State == "cancelled";
  }

  private sealed class ExportException : Exception
  {
    public readonly string Code;

    public ExportException(string code, string message)
      : base(message)
    {
      Code = code;
    }
  }
}
