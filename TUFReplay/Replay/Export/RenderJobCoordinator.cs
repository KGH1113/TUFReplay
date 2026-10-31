using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.Ipc.Generated;

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
    var result = OrbitRenderClient.StartReplay(manifestPath, BuildOptionsJson(job.Options));
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

  private static void Finish(Job job, string state, string outputFile)
  {
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

  private static string BuildOptionsJson(RenderRequest options) =>
    JsonConvert.SerializeObject(
      new
      {
        targetFps = options.TargetFps,
        videoFps = options.VideoFps,
        width = options.Width,
        height = options.Height,
        bitrateMbps = options.BitrateMbps,
        videoCodec = options.VideoCodec,
        bitDepth = options.BitDepth,
        captureAudio = options.CaptureAudio,
        audioGainDb = options.AudioGainDb,
        endDelaySeconds = options.EndDelaySeconds,
        bgaMode = options.BgaMode,
        showPlanetRings = options.ShowPlanetRings,
        showSongTitle = options.ShowSongTitle,
        showCountdown = options.ShowCountdown,
        showResultText = options.ShowResultText,
        showHitJudgments = options.ShowHitJudgments,
        // The companion render dialog always keeps OrbitRender's own overlay.
        showModOverlay = true,
      }
    );

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
    if (options.BitrateMbps < 1 || options.BitrateMbps > 200)
      throw Domain("render_options_invalid", "Bitrate must be between 1 and 200 Mbps.");
    if (options.BitDepth != 8 && options.BitDepth != 10)
      throw Domain("render_options_invalid", "Bit depth must be 8 or 10.");
    if (
      string.IsNullOrWhiteSpace(options.VideoCodec)
      || !VideoCodecs.Contains(options.VideoCodec, StringComparer.OrdinalIgnoreCase)
    )
      throw Domain("render_options_invalid", "Choose a supported video codec.");
  }

  private static readonly string[] VideoCodecs = { "H264", "H265", "VP9", "AV1", "ProRes", "FFV1" };

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

    public bool IsTerminal => State == "completed" || State == "failed" || State == "cancelled";
  }
}
