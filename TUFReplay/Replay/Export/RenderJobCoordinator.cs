using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Playback;
using TUFReplay.ModApi.Repositories;
using TUFReplay.Replay.Rendering;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.Ipc.Generated;
using TUFReplay.Shared.Media;

namespace TUFReplay.Replay.Export;

// TUFReplay owns the render job; OrbitRender only opens the recorded level and
// renders. The job exports a bundle, hands its manifest to OrbitRender, mirrors
// OrbitRender's state, and deletes the bundle once the render is terminal.
//
// Tick runs on Unity's main thread. Start/Cancel are registered as main-thread
// IPC methods so every mutation is serialized with Tick.
public static class RenderJobCoordinator
{
  public static event Action<RenderJobState> Changed;

  public const string StageExporting = "exporting_recording";
  public const string StageOpeningLevel = "opening_level";
  public const string StagePreparing = "preparing_render";
  public const string StageWaitingForConfirmation = "waiting_for_confirmation";
  public const string StageRendering = "rendering";
  public const string StageCompositing = "compositing";
  public const string StageCancelling = "cancelling";

  private const double ExportWeight = 0.1;
  private const double PrepareProgress = 0.12;
  private const double RenderProgressBase = 0.18;
  private const double RenderProgressWeight = 0.72;
  private const double CompositingProgress = 0.92;

  private static readonly object Gate = new object();
  private static readonly TimeSpan TerminalRetention = TimeSpan.FromHours(24);
  private static readonly long PollInterval = Stopwatch.Frequency / 4;

  private static Job _job;
  private static RenderJobState _published;
  private static long _nextPoll;

  public static void Initialize()
  {
    RenderBundleExportService.SweepOrphans();
  }

  public static void Shutdown()
  {
    Job job;
    lock (Gate)
    {
      job = _job;
      _job = null;
      _published = null;
    }
    if (job == null)
      return;
    ReleaseHost(job);
    if (!job.IsTerminal && !string.IsNullOrEmpty(job.OrbitJobId))
      OrbitRenderClient.Cancel(job.OrbitJobId);
    if (job.IsTerminal && !string.IsNullOrEmpty(job.BundleJobId))
      RenderBundleExportService.Discard(job.BundleJobId);
    // Anything still exporting cleans up after itself when its token fires.
    RenderBundleExportService.Shutdown();
  }

  public static RenderJobState Start(string runId, RenderRequest options, string levelPath)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw Domain("invalid_run_id", "Choose a play record to render.");
    ValidateOptions(options);
    lock (Gate)
    {
      if (_job != null)
      {
        if (!_job.IsTerminal)
          throw Domain("render_busy", "A render is already in progress. Wait for it to finish or cancel it.");
        _job = null;
        _published = null;
      }
      var bundle = RenderBundleExportService.Start(runId, levelPath, options.IncludeWebcam, options.IncludeMicrophone);
      var job = new Job
      {
        Id = bundle.JobId,
        RunId = runId,
        BundleJobId = bundle.JobId,
        Options = options,
        State = "exporting",
        Stage = StageExporting,
        Progress = ExportWeight * Clamp01(bundle.Progress),
        RunStates = Task.Run(() => LoadRunStates(runId)),
      };
      _job = job;
      _nextPoll = 0;
      Publish(job);
      return Status(job);
    }
  }

  public static RenderJobState Get()
  {
    lock (Gate)
    {
      if (_job == null)
        return null;
      if (_job.IsTerminal && _job.FinishedAtUtc < DateTime.UtcNow - TerminalRetention)
      {
        _job = null;
        _published = null;
        return null;
      }
      return Status(_job);
    }
  }

  public static bool OpenOutput(string jobId, bool reveal)
  {
    string path;
    lock (Gate)
    {
      if (_job == null || _job.Id != jobId || _job.State != "completed" || string.IsNullOrEmpty(_job.OutputFile))
        throw Domain("render_job_not_found", "The completed render is no longer available.");
      path = _job.OutputFile;
    }
    if (!File.Exists(path))
      throw Domain("render_output_missing", "The rendered video no longer exists.");
    try
    {
      var windows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.Windows
      );
      var mac = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.OSX
      );
      var start = new ProcessStartInfo { UseShellExecute = true };
      if (windows && reveal)
      {
        start.FileName = "explorer.exe";
        start.Arguments = "/select,\"" + path + "\"";
      }
      else if (windows)
        start.FileName = path;
      else
      {
        start.FileName = mac ? "/usr/bin/open" : "xdg-open";
        var target = reveal && !mac ? Path.GetDirectoryName(path) : path;
        start.Arguments =
          (mac && reveal ? "-R " : "") + "\"" + target.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
      }
      Process.Start(start);
      return true;
    }
    catch (Exception)
    {
      throw Domain("render_output_open_failed", "Could not open the rendered file. Check the computer running ADOFAI.");
    }
  }

  public static AdofaiIpc.IpcFile ReadVideo(string jobId)
  {
    lock (Gate)
    {
      if (_job == null || _job.Id != jobId || _job.State != "completed" || string.IsNullOrEmpty(_job.OutputFile))
        throw Domain("render_job_not_found", "The completed render is no longer available.");
      var path = _job.OutputFile;
      if (!File.Exists(path))
        throw Domain("render_output_missing", "The rendered video no longer exists.");
      var extension = Path.GetExtension(path).ToLowerInvariant();
      var mime =
        extension == ".webm" ? "video/webm"
        : extension == ".mov" ? "video/quicktime"
        : extension == ".mkv" ? "video/x-matroska"
        : "video/mp4";
      return AdofaiIpc.IpcFile.FromPath(path, mime);
    }
  }

  public static RenderJobState Cancel(string jobId)
  {
    if (string.IsNullOrWhiteSpace(jobId))
      throw Domain("invalid_render_job_id", "Choose a render to cancel.");
    lock (Gate)
    {
      var job = _job;
      if (job == null || !string.Equals(job.Id, jobId, StringComparison.Ordinal))
        throw Domain("render_job_not_found", "This render is no longer available. Start rendering again.");
      if (job.IsTerminal)
        return Status(job);
      job.CancelRequested = true;
      job.Stage = StageCancelling;
      if (job.State == "exporting")
      {
        try
        {
          RenderBundleExportService.Cancel(job.BundleJobId);
        }
        catch (AdofaiIpc.IpcCallException)
        {
          Finish(job, "cancelled", null);
          return Status(job);
        }
      }
      else if (!string.IsNullOrEmpty(job.OrbitJobId))
      {
        var result = OrbitRenderClient.Cancel(job.OrbitJobId);
        if (!result.Ok && result.ErrorCode == "render_job_unknown")
        {
          Finish(job, "cancelled", null);
          return Status(job);
        }
      }
      Publish(job);
      return Status(job);
    }
  }

  public static void Tick()
  {
    lock (Gate)
    {
      var job = _job;
      if (job == null || job.IsTerminal)
        return;
      long now = Stopwatch.GetTimestamp();
      if (now < _nextPoll)
        return;
      _nextPoll = now + PollInterval;
      try
      {
        if (job.State == "exporting")
          AdvanceExport(job);
        else
          AdvanceRender(job);
      }
      catch (Exception exception)
      {
        Main.Instance?.LogException("Render/Job", exception);
        Fail(job, "render_failed", "The render failed unexpectedly. Check the game log and try again.", null);
      }
    }
  }

  private static void AdvanceExport(Job job)
  {
    RenderBundleStatus bundle;
    try
    {
      bundle = RenderBundleExportService.GetStatus(job.BundleJobId);
    }
    catch (AdofaiIpc.IpcCallException)
    {
      Fail(job, "render_export_lost", "The recording preparation expired. Start rendering again.", null);
      return;
    }

    switch (bundle.State)
    {
      case "completed":
        if (job.CancelRequested)
        {
          Finish(job, "cancelled", null);
          return;
        }
        // The mod states load alongside the export and are nearly always ready first.
        if (!job.RunStates.IsCompleted)
          return;
        if (OrbitRenderClient.SupportsHostedReplay)
        {
          // TUFReplay replays the recording itself; reading it back stays off the game thread.
          string manifestPath = bundle.ManifestPath;
          job.Recording ??= Task.Run(() => RecordingBundle.Load(manifestPath));
          if (!job.Recording.IsCompleted)
            return;
          BeginHostedRender(job);
          return;
        }
        BeginRender(job, bundle.ManifestPath);
        return;
      case "failed":
        Fail(job, bundle.ErrorCode, bundle.ErrorMessage, bundle.ErrorDetails);
        return;
      case "cancelled":
        Finish(job, "cancelled", null);
        return;
      default:
        job.State = "exporting";
        job.Stage = job.CancelRequested ? StageCancelling : StageExporting;
        job.Progress = ExportWeight * Clamp01(bundle.Progress);
        Publish(job);
        return;
    }
  }

  private static void BeginRender(Job job, string manifestPath)
  {
    if (string.IsNullOrEmpty(manifestPath))
    {
      Fail(job, "render_manifest_missing", "The recording bundle is incomplete. Start rendering again.", null);
      return;
    }
    // Mods restore the state saved with this run before OrbitRender opens the level.
    job.RunStateSession = ReplaySessions.Begin(
      job.RunId,
      new ReplayRender(
        job.Options.Width,
        job.Options.Height,
        job.Options.VideoFps,
        takesOverlayVideos: false,
        ffmpegPath: SharedFfmpegPath()
      ),
      job.RunStates.Result
    );
    var result = OrbitRenderClient.StartReplay(
      manifestPath,
      BuildOptions(job.Options, OrbitRenderClient.SupportsBitrateKbps)
    );
    if (!result.Ok)
    {
      Fail(job, result.ErrorCode, result.ErrorMessage, ToDetails(result.Error));
      return;
    }
    job.OrbitJobId = result.Job.Id;
    job.State = "preparing";
    job.Stage = StageOpeningLevel;
    job.Progress = Math.Max(job.Progress, PrepareProgress);
    Publish(job);
  }

  // The FFmpeg TUFReplay shares with OrbitRender, for mods that write overlay videos; null until installed.
  private static string SharedFfmpegPath()
  {
    FfmpegInstallState ffmpeg = FfmpegInstallCoordinator.Status();
    return ffmpeg.Available && !string.IsNullOrEmpty(ffmpeg.Path) ? ffmpeg.Path : null;
  }

  // OrbitRender 3 and later: OrbitRender captures and encodes, TUFReplay replays as its host.
  private static void BeginHostedRender(Job job)
  {
    RecordingBundle recording;
    RenderReplayHost host;
    try
    {
      recording = job.Recording.GetAwaiter().GetResult();
      host = new RenderReplayHost(recording, job.Options.EndDelaySeconds);
    }
    catch (RecordingFormatException exception)
    {
      Fail(
        job,
        exception.Code,
        exception.Message,
        new RenderErrorDetails
        {
          Field = exception.Field,
          File = exception.File,
          Line = exception.Line,
        }
      );
      return;
    }
    job.Host = host;
    // Mods restore the state saved with this run before OrbitRender opens the level, and learn
    // there whether their own videos can go over the finished one.
    job.RunStateSession = ReplaySessions.Begin(
      job.RunId,
      new ReplayRender(
        job.Options.Width,
        job.Options.Height,
        job.Options.VideoFps,
        OrbitRenderClient.SupportsOverlayVideos,
        SharedFfmpegPath()
      ),
      job.RunStates.Result
    );
    var result = OrbitRenderClient.StartHostedReplay(
      HostedRequestJson(recording, BuildOptions(job.Options, OrbitRenderClient.SupportsBitrateKbps)),
      host
    );
    if (!result.Ok)
    {
      Fail(job, result.ErrorCode, result.ErrorMessage, ToDetails(result.Error));
      return;
    }
    job.OrbitJobId = result.Job.Id;
    job.State = "preparing";
    job.Stage = StageOpeningLevel;
    job.Progress = Math.Max(job.Progress, PrepareProgress);
    Publish(job);
  }

  // What OrbitRender needs besides the host: where the level is, how to name the file,
  // when the audio starts, and the camera and microphone to compose, with absolute paths.
  internal static string HostedRequestJson(RecordingBundle recording, OrbitRenderOptions options)
  {
    RecordingManifest manifest = recording.Manifest;
    var media = manifest.Media == null ? null : (JObject)manifest.Media.DeepClone();
    foreach (string name in new[] { "webcam", "microphone" })
      if (media?[name] is JObject item && item["path"]?.Type == JTokenType.String)
        item["path"] = recording.ResolveFile((string)item["path"]);
    var serializer = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
    var request = new JObject
    {
      ["levelPath"] = recording.ResolveLevelPath(),
      ["levelFileSha256"] = manifest.Level.FileSha256,
      ["startTile"] = manifest.Replay.StartTile,
      ["gameInputOffsetMs"] = manifest.Replay.GameInputOffsetMs,
      ["wonTimeUs"] = manifest.Replay.WonTimeUs,
      ["media"] = media,
      ["warnings"] = JArray.FromObject(manifest.Warnings ?? Array.Empty<string>()),
      ["options"] = JObject.FromObject(options ?? new OrbitRenderOptions(), serializer),
    };
    return request.ToString(Formatting.None);
  }

  private static void ReleaseHost(Job job)
  {
    job.RunStateSession?.End();
    // OrbitRender restores the game itself; this covers a job that ended without it.
    job.Host?.RestoreAfterRender();
    job.Host = null;
  }

  private static void AdvanceRender(Job job)
  {
    var result = OrbitRenderClient.ReadStatus(job.OrbitJobId);
    if (!result.Ok)
    {
      Fail(job, result.ErrorCode, result.ErrorMessage, ToDetails(result.Error));
      return;
    }
    var orbit = result.Job;
    job.WaitingForGameFocus = orbit.WaitingForGameFocus;
    if (orbit.Warnings != null && orbit.Warnings.Length > 0)
      job.Warnings = orbit.Warnings;

    switch (orbit.State)
    {
      case "completed":
        Finish(job, "completed", orbit.OutputPath);
        return;
      case "failed":
        Fail(job, orbit.ErrorCode, orbit.ErrorMessage, ToDetails(orbit.ErrorDetails));
        return;
      case "cancelled":
        Finish(job, "cancelled", null);
        return;
      case "rendering":
        job.State = "rendering";
        job.Stage = job.CancelRequested ? StageCancelling : StageRendering;
        job.Progress = Math.Max(job.Progress, RenderProgressBase + RenderProgressWeight * Clamp01(orbit.Progress));
        break;
      case "finishing":
        job.State = "compositing";
        job.Stage = job.CancelRequested ? StageCancelling : StageCompositing;
        job.Progress = Math.Max(job.Progress, CompositingProgress);
        break;
      case "preparing":
        job.State = "preparing";
        job.Stage = job.CancelRequested ? StageCancelling : StagePreparing;
        job.Progress = Math.Max(job.Progress, PrepareProgress);
        break;
      case "awaitingconfirmation":
        job.State = "preparing";
        job.Stage = StageWaitingForConfirmation;
        job.Progress = Math.Max(job.Progress, PrepareProgress);
        break;
      default:
        job.State = "preparing";
        job.Stage = job.CancelRequested ? StageCancelling : StageOpeningLevel;
        job.Progress = Math.Max(job.Progress, PrepareProgress);
        break;
    }
    Publish(job);
  }

  private static IReadOnlyList<StoredRunState> LoadRunStates(string runId)
  {
    try
    {
      return RunStateRepository.Load(runId);
    }
    catch (Exception exception)
    {
      // Mods then see the run as one without saved state; the render itself goes on.
      Main.Instance?.Log("[Render/RunState] Saved mod states unavailable. error=" + exception.Message);
      return Array.Empty<StoredRunState>();
    }
  }

  private static void Finish(Job job, string state, string outputFile)
  {
    ReleaseHost(job);
    job.State = state;
    job.Stage = null;
    job.WaitingForGameFocus = false;
    job.FinishedAtUtc = DateTime.UtcNow;
    if (state == "completed")
    {
      job.Progress = 1;
      job.OutputFile = outputFile;
      job.ErrorCode = null;
      job.ErrorMessage = null;
      job.ErrorDetails = null;
    }
    RenderBundleExportService.Discard(job.BundleJobId);
    Publish(job);
  }

  private static void Fail(Job job, string code, string message, RenderErrorDetails details)
  {
    ReleaseHost(job);
    job.State = "failed";
    job.Stage = null;
    job.WaitingForGameFocus = false;
    job.ErrorCode = string.IsNullOrEmpty(code) ? "render_failed" : code;
    job.ErrorMessage = string.IsNullOrEmpty(message) ? "The render failed. Check the game log and try again." : message;
    job.ErrorDetails = details;
    job.FinishedAtUtc = DateTime.UtcNow;
    RenderBundleExportService.Discard(job.BundleJobId);
    Publish(job);
  }

  private static void Publish(Job job)
  {
    var state = Status(job);
    if (_published != null && SameStatus(_published, state))
      return;
    _published = state;
    Changed?.Invoke(state);
  }

  private static RenderJobState Status(Job job) =>
    new RenderJobState
    {
      JobId = job.Id,
      RunId = job.RunId,
      State = job.State,
      Stage = job.Stage,
      Progress = job.Progress,
      OutputFile = job.OutputFile,
      Warnings = job.Warnings,
      WaitingForGameFocus = job.WaitingForGameFocus,
      ErrorCode = job.ErrorCode,
      ErrorMessage = job.ErrorMessage,
      ErrorDetails = job.ErrorDetails,
    };

  private static bool SameStatus(RenderJobState left, RenderJobState right) =>
    left.State == right.State
    && left.Stage == right.Stage
    && left.Progress == right.Progress
    && left.OutputFile == right.OutputFile
    && left.WaitingForGameFocus == right.WaitingForGameFocus
    && left.ErrorCode == right.ErrorCode
    && left.ErrorMessage == right.ErrorMessage
    && left.ErrorDetails?.Field == right.ErrorDetails?.Field
    && left.ErrorDetails?.Line == right.ErrorDetails?.Line
    && left.ErrorDetails?.File == right.ErrorDetails?.File
    && (left.Warnings ?? Array.Empty<string>()).SequenceEqual(right.Warnings ?? Array.Empty<string>());

  private static RenderErrorDetails ToDetails(OrbitRenderError error) =>
    error == null
      ? null
      : new RenderErrorDetails
      {
        Field = error.Field,
        Line = error.Line,
        File = error.File,
      };

  // An OrbitRender without Kbps support gets the nearest whole Mbps; the web snaps to it as well.
  private static OrbitRenderOptions BuildOptions(RenderRequest options, bool takesBitrateKbps) =>
    new OrbitRenderOptions
    {
      NoPreview = true,
      OutputDirectory =
        !options.OutputDirectory.IsSpecified || string.IsNullOrWhiteSpace(options.OutputDirectory.Value)
          ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Videos",
            "A Dance of Fire and Ice",
            "TUFReplay"
          )
          : options.OutputDirectory.Value,
      TargetFps = options.TargetFps,
      VideoFps = options.VideoFps,
      Width = options.Width,
      Height = options.Height,
      BitrateKbps = takesBitrateKbps ? options.BitrateKbps : null,
      BitrateMbps = takesBitrateKbps ? null : WholeMbps(options.BitrateKbps),
      VideoCodec = options.VideoCodec,
      BitDepth = options.BitDepth,
      // The profile replaces bitrate and bit depth, so it belongs only with ProRes.
      ProResProfile = IsProRes(options) && options.ProResProfile.IsSpecified ? options.ProResProfile.Value : null,
      CaptureAudio = options.CaptureAudio,
      AudioGainDb = options.AudioGainDb,
      EndDelaySeconds = options.EndDelaySeconds,
      BgaMode = options.BgaMode,
      ShowPlanetRings = options.ShowPlanetRings,
      ShowSongTitle = options.ShowSongTitle,
      ShowCountdown = options.ShowCountdown,
      ShowResultText = options.ShowResultText,
      ShowHitJudgments = options.ShowHitJudgments,
      // The companion render dialog always keeps OrbitRender's own overlay.
      ShowModOverlay = true,
    };

  private static void ValidateOptions(RenderRequest options)
  {
    if (options == null)
      throw Domain("render_options_invalid", "Render options are required.");
    if (options.Width < 320 || options.Width > 3840)
      throw Domain("render_options_invalid", "Render width must be between 320 and 3840 pixels.");
    if (options.Height < 180 || options.Height > 2160)
      throw Domain("render_options_invalid", "Render height must be between 180 and 2160 pixels.");
    if (options.TargetFps < 15 || options.TargetFps > 1024)
      throw Domain("render_options_invalid", "Game FPS must be between 15 and 1024.");
    if (options.VideoFps < 15 || options.VideoFps > 240)
      throw Domain("render_options_invalid", "Video FPS must be between 15 and 240.");
    if (options.BitrateKbps < MinBitrateKbps || options.BitrateKbps > MaxBitrateKbps)
      throw Domain("render_options_invalid", "Bitrate must be between 1,000 and 200,000 Kbps.");
    if (options.BitDepth != 8 && options.BitDepth != 10)
      throw Domain("render_options_invalid", "Bit depth must be 8 or 10.");
    if (
      string.IsNullOrWhiteSpace(options.VideoCodec)
      || !VideoCodecs.Contains(options.VideoCodec, StringComparer.OrdinalIgnoreCase)
    )
      throw Domain("render_options_invalid", "Choose a supported video codec.");
    if (
      IsProRes(options)
      && options.ProResProfile.IsSpecified
      && !ProResProfiles.Contains(options.ProResProfile.Value, StringComparer.Ordinal)
    )
      throw Domain("render_options_invalid", "Choose a ProRes profile: Proxy, LT, Standard, HQ, 4444 or 4444 XQ.");
  }

  private const int MinBitrateKbps = 1_000;
  private const int MaxBitrateKbps = 200_000;

  private static int WholeMbps(int bitrateKbps) =>
    Math.Max(1, (int)Math.Round(bitrateKbps / 1000d, MidpointRounding.AwayFromZero));

  private static bool IsProRes(RenderRequest options) =>
    string.Equals(options.VideoCodec, "ProRes", StringComparison.OrdinalIgnoreCase);

  private static readonly string[] VideoCodecs = { "H264", "H265", "VP9", "AV1", "ProRes", "FFV1" };

  // OrbitRender's profile names (OrbitRender PR #6).
  private static readonly string[] ProResProfiles = { "Proxy", "LT", "Standard", "HQ", "4444", "4444 XQ" };

  private static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Min(1, Math.Max(0, value));

  private static AdofaiIpc.IpcCallException Domain(string code, string message) => IpcDomainError.Create(code, message);

  private sealed class Job
  {
    public string Id;
    public string RunId;
    public string BundleJobId;
    public string OrbitJobId;
    public RenderRequest Options;
    public string State;
    public string Stage;
    public double Progress;
    public string OutputFile;
    public string[] Warnings = Array.Empty<string>();
    public bool WaitingForGameFocus;
    public string ErrorCode;
    public string ErrorMessage;
    public RenderErrorDetails ErrorDetails;
    public bool CancelRequested;
    public DateTime FinishedAtUtc;
    public Task<IReadOnlyList<StoredRunState>> RunStates;
    public ReplaySession RunStateSession;
    public Task<RecordingBundle> Recording;
    public RenderReplayHost Host;

    public bool IsTerminal => State == "completed" || State == "failed" || State == "cancelled";
  }
}
