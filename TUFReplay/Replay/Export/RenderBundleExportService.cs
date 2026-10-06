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
  public static event Action<object> Changed;

  public static object[] Snapshots()
  {
    lock (Gate)
      return Jobs.Values.Select(StatusLocked).ToArray();
  }

  private static void Notify(Job job)
  {
    object state;
    lock (Gate)
      state = StatusLocked(job);
    Changed?.Invoke(state);
  }

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
      if (run.EngineId != ReplayFormat.EngineId || run.FormatVersion != ReplayFormat.FormatVersion)
        throw new ExportException(
          "render_recording_version_unsupported",
          "This recording uses an older or unsupported replay format. Record a new run with the current TUFReplay version."
        );
      if (!string.IsNullOrEmpty(run.ReplayUnavailableReason))
        throw new ExportException(
          "render_recording_unavailable",
          "This run's input recording was incomplete. Check the recording failure in the activity list, then record a new run."
        );
      if (run.StartTile < 0)
        throw new ExportException(
          "render_start_tile_invalid",
          "The recording has a negative start tile. Record a new run before rendering."
        );
      ReplayMetadata meta;
      try
      {
        meta = JsonConvert.DeserializeObject<ReplayMetadata>(run.MetaJson ?? "null");
      }
      catch (JsonException exception)
      {
        throw new RenderBundleValidationException(
          "render_metadata_invalid",
          "The recording metadata could not be read. Export it again or record a new run.",
          (exception as JsonSerializationException)?.Path
        );
      }
      RenderBundleValidation.ValidateMetadata(meta);
      List<RecordedInput> inputs;
      List<ReplayHitContext> hits;
      try
      {
        inputs = ReplayInputParser.Parse(run.InputCsv);
      }
      catch (InvalidDataException exception)
      {
        throw new RenderBundleValidationException(
          exception.Data["code"] as string ?? "render_input_payload_invalid",
          exception.Message,
          field: exception.Data["field"] as string,
          line: exception.Data["line"] as int?,
          file: "inputs.csv"
        );
      }
      try
      {
        hits = ReplayHitContextParser.Parse(run.HitContextCsv);
      }
      catch (InvalidDataException exception)
      {
        throw new RenderBundleValidationException(
          "render_hit_payload_invalid",
          exception.Message,
          line: exception.Data["line"] as int?,
          file: "hits.csv"
        );
      }
      long terminalTimeUs = RenderBundleValidation.ResolveTerminalTimeUs(
        meta,
        run.Result,
        inputs,
        hits,
        out bool recoveredTerminal
      );
      string[] warnings = recoveredTerminal
        ? new[] { RenderBundleValidation.RecoveredTerminalWarning }
        : Array.Empty<string>();
      if (recoveredTerminal)
        Main.Instance?.Log(
          "[Render/Export] Recovered pre-start terminal. runId="
            + run.Id
            + ", terminalTimeUs="
            + terminalTimeUs
            + ". "
            + warnings[0]
        );
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
      Directory.CreateDirectory(job.Directory);
      using (var writer = new StreamWriter(Path.Combine(job.Directory, "inputs.csv"), false, new UTF8Encoding(false)))
        RenderBundleCsv.WriteInputs(writer, inputs, meta.inputNativePlatform, terminalTimeUs, token);
      using (var writer = new StreamWriter(Path.Combine(job.Directory, "hits.csv"), false, new UTF8Encoding(false)))
        RenderBundleCsv.WriteHits(writer, hits, terminalTimeUs, value => Enum.GetName(typeof(HitMargin), value), token);
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
              flipVertical = job.Settings.WebcamFlipVertical,
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
            result = run.Result?.ToLowerInvariant(),
            wonTimeUs = meta.wonTimeUs,
            terminalTimeUs,
          },
          inputsFile = "inputs.csv",
          hitsFile = "hits.csv",
          warnings,
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
        var validation = exception as RenderBundleValidationException;
        job.ErrorCode =
          validation?.Code
          ?? (
            exception is UnauthorizedAccessException ? "render_export_access_denied"
            : exception is IOException ? "render_export_io"
            : "render_export_failed"
          );
        job.ErrorDetails =
          validation == null
            ? null
            : new
            {
              field = validation.Field,
              line = validation.Line,
              file = validation.File,
            };
        job.ErrorMessage =
          validation != null ? exception.Message
          : exception is UnauthorizedAccessException
            ? "The recording files cannot be accessed. Check folder permissions and try again."
          : exception is IOException
            ? "The recording files could not be read or written. Check that the files are available and the disk has enough free space, then try again."
          : "The recording could not be prepared. Check the level file and available disk space, then try again.";
      }
    }
    finally
    {
      Notify(job);
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
    Notify(job);
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
      errorDetails = job.ErrorDetails,
    };

  private static object Error(string code, string message) => TUFReplay.Shared.Ipc.IpcDomainError.Create(code, message);

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
    public object ErrorDetails;
    public readonly DateTime CreatedAt = DateTime.UtcNow;
    public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
    public bool Finished => State == "completed" || State == "failed" || State == "cancelled";
  }

  private sealed class ExportException : RenderBundleValidationException
  {
    public ExportException(string code, string message)
      : base(code, message) { }
  }
}
