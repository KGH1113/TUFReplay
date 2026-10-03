using System;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Settings;
using TUFReplay.Webcam.Repositories;
using TUFReplay.Webcam.Timing;
using UnityEngine;
using UnityEngine.Video;

namespace TUFReplay.Webcam.Playback;

public sealed class WebcamReplayPlayer : IReplayWebcamPlayer
{
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
    bool outside = target < 0 || target >= _lease.Recording.DurationUs / 1_000_000d;
    if (_hasTarget && (target < _target - 0.05 || target - _target > Time.unscaledDeltaTime * rate + 0.2))
      _forceSeek = true;
    if (_wasPaused != snapshot.Paused || _lastOffset != settings.WebcamOffsetMs)
      _forceSeek = true;
    _hasTarget = true;
    _wasPaused = snapshot.Paused;
    _lastOffset = settings.WebcamOffsetMs;
    _target = Math.Max(
      0,
      Math.Min(target, Math.Max(0, _lease.Recording.DurationUs / 1_000_000d - 1d / _lease.Recording.FrameRate))
    );

    if (!settings.WebcamPlaybackVisible || outside)
    {
      _overlay.ShowFrame = false;
      if (_preparing || _video.isPrepared)
        _video.Stop();
      _preparing = false;
      _seeking = false;
      _forceSeek = true;
      _lastPlaybackRate = float.NaN;
      return;
    }

    if (!_video.isPrepared)
    {
      _overlay.ShowFrame = false;
      if (!_preparing)
      {
        _preparing = true;
        _prepareStartedAt = Time.realtimeSinceStartupAsDouble;
        _video.Prepare();
      }
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
    if (_forceSeek && !_seeking && !outside)
    {
      if (!_video.canSetTime)
      {
        OnVideoError(_video, "This video cannot be synchronized with the replay.");
        return;
      }
      _video.Pause();
      _seeking = true;
      _lastRequested = _target;
      _seekStartedAt = Time.realtimeSinceStartupAsDouble;
      _forceSeek = false;
      _video.time = _target;
    }
    _overlay.ShowFrame = !outside && settings.WebcamPlaybackVisible && !_seeking;
    if (_seeking || snapshot.Paused || outside)
    {
      if (_video.isPlaying)
        _video.Pause();
    }
    else if (!_video.isPlaying)
      _video.Play();
  }

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
    _seeking = false;
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
    _video.Stop();
    UnityEngine.Object.Destroy(_object);
    _lease.Dispose();
  }
}
