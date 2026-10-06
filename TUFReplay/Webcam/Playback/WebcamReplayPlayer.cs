using System;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Capture;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Threading;
using TUFReplay.Webcam.Repositories;
using TUFReplay.Webcam.Timing;
using UnityEngine;
using UnityEngine.Video;

namespace TUFReplay.Webcam.Playback;

public sealed class WebcamReplayPlayer : IReplayWebcamPlayer
{
  private static readonly SerialBackgroundQueue Logs = new SerialBackgroundQueue();
  private readonly WebcamRecordingLease _lease;
  private readonly GameObject _object;
  private readonly VideoPlayer _video;
  private readonly WebcamReplayOverlay _overlay;
  private double _prepareStartedAt;
  private bool _preparing;
  private float _lastPlaybackRate = float.NaN;
  private bool _disposed;
  private bool _failed;
  private bool _forceSeek = true;
  private bool _seeking;
  private bool _hasTarget;
  private bool _wasPaused;
  private double _target;
  private double _lastRequested;
  private double _seekStartedAt;
  private int _lastOffset;
  private double _previousRawTarget;
  private bool _hasDecodedFrame;
  private bool _displayReported;

  public WebcamReplayPlayer(WebcamRecordingLease lease)
  {
    _lease = lease ?? throw new ArgumentNullException(nameof(lease));
    _object = new GameObject("TUFReplay.WebcamReplay");
    try
    {
      UnityEngine.Object.DontDestroyOnLoad(_object);
      _video = _object.AddComponent<VideoPlayer>();
      _overlay = _object.AddComponent<WebcamReplayOverlay>();
      _overlay.Initialize(_video, lease.Recording.Width / (double)lease.Recording.Height);
      _video.playOnAwake = false;
      _video.isLooping = false;
      _video.audioOutputMode = VideoAudioOutputMode.None;
      _video.renderMode = VideoRenderMode.APIOnly;
      _video.source = VideoSource.Url;
      _video.url = new Uri(lease.Recording.FilePath).AbsoluteUri;
      _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
      _video.timeReference = VideoTimeReference.ExternalTime;
      _video.skipOnDrop = true;
      _video.errorReceived += OnVideoError;
      _video.seekCompleted += OnSeekCompleted;
      _video.prepareCompleted += OnPrepared;
      _video.frameReady += OnFrameReady;
      if (TUFReplaySettingStore.Current.WebcamPlaybackVisible)
        BeginPreparation();
    }
    catch
    {
      UnityEngine.Object.Destroy(_object);
      throw;
    }
  }

  public void Tick(ReplayPlaybackSnapshot snapshot)
  {
    if (_disposed || _failed)
      return;
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    double target = WebcamPlaybackClock.ToVideoSeconds(snapshot, _lease.Recording, settings.WebcamOffsetMs);
    double rate = WebcamPlaybackClock.PlaybackRate(snapshot, _lease.Recording);
    bool before = target < 0;
    bool after = target >= _lease.Recording.DurationUs / 1_000_000d;
    bool outside = before || after;
    if (
      _hasTarget
      && (target < _previousRawTarget - 0.05 || target - _previousRawTarget > Time.unscaledDeltaTime * rate + 0.2)
    )
      _forceSeek = true;
    if (_wasPaused != snapshot.Paused || _lastOffset != settings.WebcamOffsetMs)
      _forceSeek = true;
    _hasTarget = true;
    _previousRawTarget = target;
    _wasPaused = snapshot.Paused;
    _lastOffset = settings.WebcamOffsetMs;
    _target = Math.Max(
      0,
      Math.Min(target, Math.Max(0, _lease.Recording.DurationUs / 1_000_000d - 1d / _lease.Recording.FrameRate))
    );

    if (!settings.WebcamPlaybackVisible || after)
    {
      _overlay.ShowFrame = false;
      if (_preparing || _video.isPrepared)
        _video.Stop();
      _preparing = false;
      _seeking = false;
      _forceSeek = true;
      _lastPlaybackRate = float.NaN;
      _hasDecodedFrame = false;
      _displayReported = false;
      return;
    }

    if (!_video.isPrepared)
    {
      _overlay.ShowFrame = false;
      if (!_preparing)
        BeginPreparation();
      if (Time.realtimeSinceStartupAsDouble - _prepareStartedAt > 15)
        OnVideoError(_video, "Video preparation timed out.");
      return;
    }
    _preparing = false;
    if (
      _seeking
      && Time.realtimeSinceStartupAsDouble - _seekStartedAt > 1
      && _video.texture != null
      && Math.Abs(_video.time - _lastRequested) <= 2d / _lease.Recording.FrameRate
    )
      OnSeekCompleted(_video);
    if (_seeking && Time.realtimeSinceStartupAsDouble - _seekStartedAt > 8)
    {
      OnVideoError(_video, "Video synchronization timed out.");
      return;
    }
    _video.externalReferenceTime = _target;
    float playbackRate = (float)rate;
    if (playbackRate != _lastPlaybackRate && _video.canSetPlaybackSpeed)
    {
      _video.playbackSpeed = playbackRate;
      _lastPlaybackRate = playbackRate;
    }
    if (_forceSeek && !_seeking)
    {
      if (!_video.canSetTime)
      {
        OnVideoError(_video, "This video cannot be synchronized with the replay.");
        return;
      }
      _forceSeek = false;
      if (!_hasDecodedFrame || Math.Abs(_video.time - _target) > 0.5d / _lease.Recording.FrameRate)
      {
        _video.Pause();
        _seeking = true;
        _lastRequested = _target;
        _seekStartedAt = Time.realtimeSinceStartupAsDouble;
        _video.time = _target;
        Record(
          "playback.seek.begin",
          new
          {
            runId = _lease.Recording.RunId,
            targetSeconds = _target,
            beforeVideoStart = before,
          }
        );
      }
    }
    _overlay.ShowFrame = !outside && settings.WebcamPlaybackVisible && !_seeking;
    if (_overlay.ShowFrame && !_displayReported && _video.texture != null)
    {
      _displayReported = true;
      Record(
        "playback.first-display",
        new
        {
          runId = _lease.Recording.RunId,
          targetSeconds = _target,
          videoSeconds = _video.time,
        }
      );
    }
    if (_seeking || snapshot.Paused || outside)
    {
      if (_video.isPlaying)
        _video.Pause();
    }
    else if (!_video.isPlaying)
      _video.Play();
  }

  private void BeginPreparation()
  {
    _preparing = true;
    _prepareStartedAt = Time.realtimeSinceStartupAsDouble;
    _hasDecodedFrame = false;
    _video.sendFrameReadyEvents = true;
    Record("playback.prepare.begin", new { runId = _lease.Recording.RunId });
    _video.Prepare();
  }

  private void OnPrepared(VideoPlayer player)
  {
    if (_disposed || _failed || !player.isPrepared)
      return;
    Record(
      "playback.prepare.complete",
      new { runId = _lease.Recording.RunId, elapsedMs = (Time.realtimeSinceStartupAsDouble - _prepareStartedAt) * 1000 }
    );
  }

  private void OnFrameReady(VideoPlayer player, long frame)
  {
    if (_disposed || _failed)
      return;
    _hasDecodedFrame = true;
    player.sendFrameReadyEvents = false;
    Record(
      "playback.first-frame-ready",
      new
      {
        runId = _lease.Recording.RunId,
        frame,
        videoSeconds = player.time,
      }
    );
    if (_seeking && Math.Abs(player.time - _lastRequested) <= 2d / _lease.Recording.FrameRate)
      OnSeekCompleted(player);
  }

  private static void Record(string name, object state) =>
    _ = Logs.Enqueue(() => CaptureDiagnostics.Record(name, state));

  public void ResetTo(ReplayPlaybackSnapshot snapshot)
  {
    _forceSeek = true;
    _hasTarget = false;
    Tick(snapshot);
  }

  public void Stop()
  {
    if (_disposed)
      return;
    _video.Pause();
    _overlay.ShowFrame = false;
    _forceSeek = true;
    _hasTarget = false;
  }

  private void OnSeekCompleted(VideoPlayer player)
  {
    if (!_seeking || _disposed || _failed)
      return;
    _seeking = false;
    Record(
      "playback.seek.complete",
      new
      {
        runId = _lease.Recording.RunId,
        elapsedMs = (Time.realtimeSinceStartupAsDouble - _seekStartedAt) * 1000,
        targetSeconds = _lastRequested,
      }
    );
    if (_wasPaused && Math.Abs(_target - _lastRequested) > 2d / _lease.Recording.FrameRate)
      _forceSeek = true;
  }

  private void OnVideoError(VideoPlayer player, string message)
  {
    if (_failed || _disposed)
      return;
    _failed = true;
    _overlay.ShowFrame = false;
    player.Stop();
    Main.Instance?.Log("[Replay/Camera] Video unavailable. error=" + message);
    ReplayTimelineHud.ShowNotificationToast(
      "Camera video unavailable",
      "The video could not be played. The replay will continue. Try another recording or update your graphics driver."
    );
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _video.errorReceived -= OnVideoError;
    _video.seekCompleted -= OnSeekCompleted;
    _video.prepareCompleted -= OnPrepared;
    _video.frameReady -= OnFrameReady;
    _video.Stop();
    UnityEngine.Object.Destroy(_object);
    _lease.Dispose();
  }
}
