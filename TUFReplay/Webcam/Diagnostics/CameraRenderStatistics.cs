using System;

namespace TUFReplay.Webcam.Diagnostics;

internal enum CameraRenderPhase
{
  OverlayUpdate,
  OverlayInput,
  PreviewTick,
  PreviewRead,
  SharedCopy,
  TextureLoad,
  TextureApply,
}

internal readonly struct CameraTimingSnapshot
{
  public readonly long Count;
  public readonly double TotalMs;
  public readonly double MeanMs;
  public readonly double MaxMs;

  internal CameraTimingSnapshot(long count, long totalTicks, long maxTicks, long frequency)
  {
    Count = count;
    TotalMs = totalTicks * 1000d / frequency;
    MeanMs = count == 0 ? 0 : TotalMs / count;
    MaxMs = maxTicks * 1000d / frequency;
  }

  internal object DiagnosticState() =>
    new
    {
      count = Count,
      totalMs = TotalMs,
      meanMs = MeanMs,
      maxMs = MaxMs,
    };
}

// Unity-thread counters only. Keep unfinished frames outside the reporting
// window so multiple overlay updates cannot be counted as multiple game frames.
internal sealed class CameraRenderStatistics
{
  private struct Timing
  {
    public long Count;
    public long TotalTicks;
    public long MaxTicks;

    public void Add(long ticks)
    {
      ticks = Math.Max(0, ticks);
      Count++;
      TotalTicks += ticks;
      MaxTicks = Math.Max(MaxTicks, ticks);
    }

    public void Merge(Timing other)
    {
      Count += other.Count;
      TotalTicks += other.TotalTicks;
      MaxTicks = Math.Max(MaxTicks, other.MaxTicks);
    }

    public CameraTimingSnapshot Snapshot(long frequency) => new(Count, TotalTicks, MaxTicks, frequency);
  }

  private readonly long _frequency;
  private readonly Timing[] _framePhases = new Timing[7];
  private readonly Timing[] _phases = new Timing[7];
  private Timing _gameFrames;
  private Timing _uiPerFrame;
  private Timing _uiPerVisibleFrame;
  private long _frameStart;
  private long _frameLiveCalls;
  private long _frameReplayCalls;
  private long _liveCalls;
  private long _replayCalls;
  private long _uploads;
  private long _resizes;
  private long _frameUploads;
  private long _frameResizes;
  private bool _cameraEnabled;
  private bool _liveRequested;
  private bool _inGame;
  private bool _focused;
  private bool _setupVisible;
  private long _enabledFrames;
  private long _liveRequestedFrames;
  private long _inGameFrames;
  private long _focusedFrames;
  private long _setupFrames;
  private int _lastWidth;
  private int _lastHeight;

  internal int FrameId { get; private set; } = -1;
  internal long CompletedFrames => _gameFrames.Count;
  internal double WindowSeconds => _gameFrames.TotalTicks / (double)_frequency;

  internal CameraRenderStatistics(long frequency)
  {
    if (frequency <= 0)
      throw new ArgumentOutOfRangeException(nameof(frequency));
    _frequency = frequency;
  }

  internal void BeginFrame(int frameId, long timestamp)
  {
    if (frameId == FrameId)
      return;
    if (FrameId >= 0)
    {
      _gameFrames.Add(timestamp - _frameStart);
      long uiTicks =
        _framePhases[(int)CameraRenderPhase.OverlayUpdate].TotalTicks
        + _framePhases[(int)CameraRenderPhase.OverlayInput].TotalTicks;
      _uiPerFrame.Add(uiTicks);
      if (_frameLiveCalls + _frameReplayCalls > 0)
        _uiPerVisibleFrame.Add(uiTicks);
      for (int index = 0; index < _phases.Length; index++)
        _phases[index].Merge(_framePhases[index]);
      _liveCalls += _frameLiveCalls;
      _replayCalls += _frameReplayCalls;
      _uploads += _frameUploads;
      _resizes += _frameResizes;
      _enabledFrames += _cameraEnabled ? 1 : 0;
      _liveRequestedFrames += _liveRequested ? 1 : 0;
      _inGameFrames += _inGame ? 1 : 0;
      _focusedFrames += _focused ? 1 : 0;
      _setupFrames += _setupVisible ? 1 : 0;
    }
    Array.Clear(_framePhases, 0, _framePhases.Length);
    _frameLiveCalls = _frameReplayCalls = _frameUploads = _frameResizes = 0;
    FrameId = frameId;
    _frameStart = timestamp;
  }

  internal void SetContext(bool cameraEnabled, bool liveRequested, bool inGame, bool focused, bool setupVisible)
  {
    _cameraEnabled = cameraEnabled;
    _liveRequested = liveRequested;
    _inGame = inGame;
    _focused = focused;
    _setupVisible = setupVisible;
  }

  internal void Record(CameraRenderPhase phase, long elapsedTicks) => _framePhases[(int)phase].Add(elapsedTicks);

  internal void RecordOverlayUpdate(long elapsedTicks, bool visible, bool replay)
  {
    Record(CameraRenderPhase.OverlayUpdate, elapsedTicks);
    if (visible)
    {
      if (replay)
        _frameReplayCalls++;
      else
        _frameLiveCalls++;
    }
  }

  internal void RecordUpload(int width, int height, bool resized)
  {
    _frameUploads++;
    _frameResizes += resized ? 1 : 0;
    _lastWidth = width;
    _lastHeight = height;
  }

  internal CameraTimingSnapshot Phase(CameraRenderPhase phase) => _phases[(int)phase].Snapshot(_frequency);

  internal CameraTimingSnapshot UiPerFrame => _uiPerFrame.Snapshot(_frequency);
  internal CameraTimingSnapshot UiPerVisibleFrame => _uiPerVisibleFrame.Snapshot(_frequency);

  internal object DiagnosticState() =>
    new
    {
      windowSeconds = WindowSeconds,
      completedFrames = CompletedFrames,
      gameFps = WindowSeconds > 0 ? CompletedFrames / WindowSeconds : 0,
      gameFrame = _gameFrames.Snapshot(_frequency).DiagnosticState(),
      contextFrames = new
      {
        cameraEnabled = _enabledFrames,
        liveRequested = _liveRequestedFrames,
        inGame = _inGameFrames,
        focused = _focusedFrames,
        cameraSetup = _setupFrames,
      },
      uGui = new
      {
        updates = Phase(CameraRenderPhase.OverlayUpdate).DiagnosticState(),
        pointerInput = Phase(CameraRenderPhase.OverlayInput).DiagnosticState(),
        perGameFrame = UiPerFrame.DiagnosticState(),
        perVisibleFrame = UiPerVisibleFrame.DiagnosticState(),
        liveVisibleUpdates = _liveCalls,
        replayVisibleUpdates = _replayCalls,
        hiddenUpdates = _phases[(int)CameraRenderPhase.OverlayUpdate].Count - _liveCalls - _replayCalls,
      },
      preview = new
      {
        tick = Phase(CameraRenderPhase.PreviewTick).DiagnosticState(),
        readAttempts = Phase(CameraRenderPhase.PreviewRead).DiagnosticState(),
        completedCopies = Phase(CameraRenderPhase.SharedCopy).DiagnosticState(),
        textureLoad = Phase(CameraRenderPhase.TextureLoad).DiagnosticState(),
        textureApply = Phase(CameraRenderPhase.TextureApply).DiagnosticState(),
        uploadedFrames = _uploads,
        textureResizes = _resizes,
        lastUploadedWidth = _lastWidth,
        lastUploadedHeight = _lastHeight,
      },
    };

  internal void ResetWindow()
  {
    Array.Clear(_phases, 0, _phases.Length);
    _gameFrames = _uiPerFrame = _uiPerVisibleFrame = default;
    _liveCalls = _replayCalls = _uploads = _resizes = 0;
    _enabledFrames = _liveRequestedFrames = _inGameFrames = _focusedFrames = _setupFrames = 0;
  }
}
