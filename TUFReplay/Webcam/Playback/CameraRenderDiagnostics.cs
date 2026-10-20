using System;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Composition;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.Capture;
using TUFReplay.Shared.Threading;
using TUFReplay.Webcam.Diagnostics;
using UnityEngine;

namespace TUFReplay.Webcam.Playback;

internal static class CameraRenderDiagnostics
{
  private static readonly SerialBackgroundQueue Logs = new SerialBackgroundQueue();
  private static CameraRenderStatistics _statistics;
  private static CameraEngineDiagnostics _engine;
  private static int _context = -1;

  internal static void Tick(bool setupVisible)
  {
    EnsureFrame();
    if (_engine == null)
      _engine = new CameraEngineDiagnostics();
    _engine.Poll(Time.frameCount);
    bool enabled = Main.Settings?.WebcamEnabled == true;
    bool live = Main.Settings?.WebcamLiveVisible == true;
    bool inGame = ADOBase.isScnGame || (scnEditor.instance != null && scnEditor.instance.playMode);
    bool focused = Application.isFocused;
    bool replay = ReplaySessionService.HasActiveContext;
    int context =
      (enabled ? 1 : 0)
      | (live ? 2 : 0)
      | (inGame ? 4 : 0)
      | (focused ? 8 : 0)
      | (setupVisible ? 16 : 0)
      | (replay ? 32 : 0);
    if (_context != context)
    {
      // The just-completed frame still belongs to the previous context.
      Flush("context_changed");
      _context = context;
      var state = new
      {
        frameId = Time.frameCount,
        utc = DateTime.UtcNow.ToString("O"),
        context = ContextSnapshot(),
      };
      _ = Logs.Enqueue(() => CaptureDiagnostics.Record("render.context", state));
    }
    // Flush only completed frames, before this frame's preview/UI work.
    if (_statistics.WindowSeconds >= 5)
      Flush("interval");
    _statistics.SetContext(enabled, live, inGame, focused, setupVisible);
  }

  private static object ContextSnapshot() =>
    new
    {
      cameraEnabled = (_context & 1) != 0,
      liveRequested = (_context & 2) != 0,
      inGame = (_context & 4) != 0,
      focused = (_context & 8) != 0,
      cameraSetup = (_context & 16) != 0,
      replay = (_context & 32) != 0,
      mode = (_context & 1) == 0 ? "camera_off"
      : (_context & 2) == 0 ? "capture_without_live_overlay"
      : "capture_with_live_overlay_requested",
    };

  private static void EnsureFrame()
  {
    if (_statistics == null)
      _statistics = new CameraRenderStatistics(Stopwatch.Frequency);
    int frameId = Time.frameCount;
    if (_statistics.FrameId != frameId)
      _statistics.BeginFrame(frameId, Stopwatch.GetTimestamp());
  }

  internal static void Record(CameraRenderPhase phase, long elapsedTicks)
  {
    EnsureFrame();
    _statistics.Record(phase, elapsedTicks);
  }

  internal static void RecordOverlayUpdate(long elapsedTicks, bool visible, bool replay)
  {
    EnsureFrame();
    _statistics.RecordOverlayUpdate(elapsedTicks, visible, replay);
  }

  internal static void RecordUpload(int width, int height, bool resized)
  {
    EnsureFrame();
    _statistics.RecordUpload(width, height, resized);
  }

  private static void Flush(string reason)
  {
    if (_statistics.CompletedFrames == 0)
      return;
    // Snapshot immutable values here; JSON encoding and Player.log writes run
    // on a worker, outside the timed UI/copy/upload paths.
    var data = new
    {
      schemaVersion = 3,
      renderer = "ugui",
      reason,
      measuredThreadId = Thread.CurrentThread.ManagedThreadId,
      windowEndUtc = DateTime.UtcNow.ToString("O"),
      windowEndFrameId = Time.frameCount,
      stopwatchFrequency = Stopwatch.Frequency,
      timing = "game_thread_elapsed_not_gpu",
      targetFrameRate = Application.targetFrameRate,
      vSyncCount = QualitySettings.vSyncCount,
      timeScale = Time.timeScale,
      screenWidth = Screen.width,
      screenHeight = Screen.height,
      context = ContextSnapshot(),
      capture = new
      {
        readyAtWindowEnd = FeatureRegistry.WebcamRecording?.IsReady == true,
        deviceId = Main.Settings?.WebcamDeviceId,
        quality = Main.Settings?.WebcamQuality,
      },
      metrics = _statistics.DiagnosticState(),
      engine = _engine?.SnapshotAndReset(),
    };
    _statistics.ResetWindow();
    _ = Logs.Enqueue(() => CaptureDiagnostics.Record("render.performance", data));
  }

  internal static void Shutdown()
  {
    if (_statistics == null)
      return;
    Flush("shutdown");
    _engine?.Dispose();
    _engine = null;
    _statistics = null;
    _context = -1;
  }
}
