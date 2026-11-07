using System;
using System.IO;
using System.Threading;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Processing;
using TUFReplay.Replay.Playback;
using UnityEngine;

namespace TUFReplay.Microphone.Playback;

// Plays a run's microphone recording during an in-game replay. A silent
// carrier source keeps Unity mixing, and ReplayMicrophoneRenderer writes the
// recording into each of its audio blocks at the WAV position that block's
// DSP time maps to. The main thread only publishes that mapping.
public sealed class ReplayMicrophonePlayer : IReplayMicrophonePlayer
{
  // A playing schedule moves once the replay timeline maps this far from it,
  // for example across a pause section of the recording.
  private const int ResyncThresholdMilliseconds = 30;

  // A reset this close to the playing schedule keeps it, so repeated resets
  // of the same position do not click.
  private const int DuplicateResetMilliseconds = 2;

  // The prefetch starts this far before a new schedule's frame, in case the
  // first mixed block is a little earlier than the main thread's DSP time.
  private const int PrimeLeadMilliseconds = 20;

  private readonly StoredMicrophoneRecording _recording;
  private readonly Pcm16WaveInfo _wave;
  private readonly ReplayMicrophoneRenderer _renderer;
  private readonly GameObject _gameObject;
  private readonly AudioSource _source;
  private readonly AudioClip _carrier;
  private readonly ReplayMicrophoneFilter _filter;
  private readonly int _outputSampleRate;
  private readonly int _resyncThresholdFrames;
  private readonly int _duplicateResetFrames;
  private readonly int _primeLeadFrames;
  private ReplayMicrophoneSchedule _schedule;
  private long _userLatencyUs;
  private float _gain;
  private bool _scheduledLogged;
  private bool _startedLogged;
  private bool _failureLogged;
  private bool _diagnosticsLogged = true;
  private bool _failed;
  private bool _disposed;
  private long _resyncCount;
  private long _duplicateResetCount;

  internal ReplayMicrophonePlayer(
    StoredMicrophoneRecording recording,
    Pcm16WaveInfo wave,
    int userOffsetMs = 0,
    MicrophoneVolume volume = default
  )
  {
    _recording = recording ?? throw new ArgumentNullException(nameof(recording));
    _wave = wave ?? throw new ArgumentNullException(nameof(wave));
    SetLatency(userOffsetMs);
    _gain = volume.Multiplier;
    _outputSampleRate = Math.Max(1, AudioSettings.outputSampleRate);
    AudioSettings.GetDSPBufferSize(out int dspBufferFrames, out _);
    dspBufferFrames = Math.Max(1, dspBufferFrames);
    _resyncThresholdFrames = Math.Max(1, wave.SampleRate * ResyncThresholdMilliseconds / 1000);
    _duplicateResetFrames = Math.Max(1, wave.SampleRate * DuplicateResetMilliseconds / 1000);
    _primeLeadFrames = Math.Max(0, wave.SampleRate * PrimeLeadMilliseconds / 1000);

    // One second of audio, so a skip after a short stall stays inside it.
    int frameBytes = checked(wave.Channels * 2);
    int capacity = Math.Max(64 * 1024, checked(wave.SampleRate * frameBytes));
    capacity -= capacity % frameBytes;
    var prefetch = new Pcm16PrefetchBuffer(
      new FileStream(recording.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read),
      wave.DataOffset,
      wave.DataLength,
      capacity
    );
    _renderer = new ReplayMicrophoneRenderer(
      prefetch,
      wave.SampleRate,
      wave.Channels,
      wave.FrameCount,
      _outputSampleRate
    );
    _renderer.Gain = _gain;
    _renderer.AudibleFromFrame = Math.Min(
      wave.FrameCount,
      MicrophonePlaybackClock.AudibleFromFrame(recording, wave.SampleRate)
    );

    try
    {
      _gameObject = new GameObject("TUFReplay Replay Microphone");
      UnityEngine.Object.DontDestroyOnLoad(_gameObject);
      _source = _gameObject.AddComponent<AudioSource>();
      _source.playOnAwake = false;
      _source.loop = true;
      _source.spatialBlend = 0f;
      _source.volume = 1f;
      // ADOFAI's master volume sets AudioListener.volume. Renders mix the mic
      // without it, so in-game playback ignores it too and only follows the
      // microphone volume.
      _source.ignoreListenerVolume = true;
      _source.pitch = 1f;
      _source.priority = 0;

      // The carrier only keeps the source mixing so Unity runs the filter for
      // every block. It has the recording's channel count, so Unity places the
      // output the same way it placed the recording's own clip.
      int carrierFrames = Math.Max(dspBufferFrames * 4, _outputSampleRate / 10);
      _carrier = AudioClip.Create(
        "TUFReplay Replay Microphone",
        carrierFrames,
        wave.Channels,
        _outputSampleRate,
        false
      );
      _carrier.SetData(new float[carrierFrames * wave.Channels], 0);
      _source.clip = _carrier;
      _filter = _gameObject.AddComponent<ReplayMicrophoneFilter>();
      _filter.Renderer = _renderer;
      _source.Play();

      Main.Instance?.Log(
        "[Replay/Microphone] Player ready. frames="
          + wave.FrameCount
          + ", sampleRate="
          + wave.SampleRate
          + ", outputSampleRate="
          + _outputSampleRate
          + ", dspBufferFrames="
          + dspBufferFrames
          + ", captureOffsetUs="
          + recording.CaptureStartOffsetUs
          + ", timelinePoints="
          + (recording.Timeline?.Points?.Length ?? 0)
          + ", audibleFromFrame="
          + MicrophonePlaybackClock.AudibleFromFrame(recording, wave.SampleRate)
          + ", volume="
          + volume
      );
    }
    catch
    {
      if (_filter != null)
        _filter.Renderer = null;
      if (_gameObject != null)
        UnityEngine.Object.Destroy(_gameObject);
      if (_carrier != null)
        UnityEngine.Object.Destroy(_carrier);
      _renderer.Dispose();
      DeletePlaybackFile();
      throw;
    }
  }

  public void ResetTo(ReplayPlaybackSnapshot snapshot)
  {
    if (_disposed || HasFailed())
      return;

    try
    {
      double now = AudioSettings.dspTime;
      double frame = FrameAtDsp(snapshot, now);
      _diagnosticsLogged = false;
      if (!ReplayMicrophonePlaybackDecisions.ShouldPlay(snapshot.Paused, snapshot.MicrophoneAudible))
      {
        Unpublish();
        _renderer.Prime((long)Math.Floor(frame) - _primeLeadFrames);
        return;
      }

      if (_schedule != null && Math.Abs(_schedule.FrameAt(now, _wave.SampleRate) - frame) <= _duplicateResetFrames)
      {
        _duplicateResetCount++;
        return;
      }
      Publish(snapshot, now, frame);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void Tick(ReplayPlaybackSnapshot snapshot)
  {
    if (_disposed || HasFailed())
      return;

    try
    {
      if (FrameAtTimeline(snapshot, snapshot.TimelineTimeUs) >= _wave.FrameCount)
      {
        Stop();
        return;
      }
      if (!ReplayMicrophonePlaybackDecisions.ShouldPlay(snapshot.Paused, snapshot.MicrophoneAudible))
      {
        Unpublish();
        return;
      }

      // An audio device change stops every source; keep the carrier mixing.
      if (!_source.isPlaying && !AudioListener.pause)
        _source.Play();
      LogFirstBlock();

      double now = AudioSettings.dspTime;
      double frame = FrameAtDsp(snapshot, now);
      if (_schedule == null)
      {
        Publish(snapshot, now, frame);
        return;
      }
      if (
        ReplayMicrophonePlaybackDecisions.ShouldReanchor(
          frame,
          _schedule.FrameAt(now, _wave.SampleRate),
          _resyncThresholdFrames
        )
      )
      {
        _resyncCount++;
        Publish(snapshot, now, frame);
      }
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void UpdateLatency(int latencyMs, ReplayPlaybackSnapshot snapshot)
  {
    if (_disposed)
      return;
    SetLatency(latencyMs);
    if (_schedule == null || HasFailed())
      return;
    try
    {
      double now = AudioSettings.dspTime;
      Publish(snapshot, now, FrameAtDsp(snapshot, now));
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void UpdateVolume(MicrophoneVolume volume)
  {
    if (_disposed)
      return;
    _gain = volume.Multiplier;
    _renderer.Gain = _gain;
  }

  public void Stop()
  {
    if (_disposed)
      return;
    Unpublish();
    LogDiagnostics();
    ResetDiagnostics();
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    LogDiagnostics();
    _disposed = true;
    _schedule = null;
    try
    {
      _filter.Renderer = null;
      _source.Stop();
      _source.clip = null;
    }
    catch { }
    if (_carrier != null)
      UnityEngine.Object.Destroy(_carrier);
    if (_gameObject != null)
      UnityEngine.Object.Destroy(_gameObject);
    ThreadPool.QueueUserWorkItem(_ =>
    {
      // The renderer waits for an audio block in progress, then stops the disk
      // reader, away from the game's frame thread.
      _renderer.Dispose();
      DeletePlaybackFile();
    });
  }

  private double FrameAtTimeline(ReplayPlaybackSnapshot snapshot, long timelineTimeUs) =>
    MicrophonePlaybackClock.ToAudioTimeUs(_recording, snapshot, timelineTimeUs, _userLatencyUs)
    * _wave.SampleRate
    / 1_000_000d;

  // The snapshot's timeline is exact at its DSP time, so the frame for any DSP
  // time is exact as well. Song audio is scheduled on the same clock.
  private double FrameAtDsp(ReplayPlaybackSnapshot snapshot, double dspTime) =>
    FrameAtTimeline(snapshot, snapshot.HasDspTime ? snapshot.TimelineTimeAtDspUs(dspTime) : snapshot.TimelineTimeUs);

  private void Publish(ReplayPlaybackSnapshot snapshot, double now, double frame)
  {
    _schedule = new ReplayMicrophoneSchedule(now, frame);
    _renderer.Prime((long)Math.Floor(frame) - _primeLeadFrames);
    _renderer.Publish(_schedule);
    _diagnosticsLogged = false;
    if (_scheduledLogged)
      return;
    _scheduledLogged = true;
    Main.Instance?.Log(
      "[Replay/Microphone] Playback scheduled. replayTimeUs="
        + snapshot.TimelineTimeUs
        + ", frame="
        + frame.ToString("0.#")
        + ", inputOffsetUs="
        + snapshot.GameInputOffsetUs
        + ", gain="
        + _gain
    );
  }

  private void Unpublish()
  {
    if (_schedule == null)
      return;
    _schedule = null;
    _renderer.Publish(null);
  }

  // How far after its schedule the first block with recorded audio was mixed,
  // and which frame it started on.
  private void LogFirstBlock()
  {
    if (_startedLogged || !_renderer.TryTakeFirstBlock(out ReplayMicrophoneFirstBlock block))
      return;
    _startedLogged = true;
    Main.Instance?.Log(
      "[Replay/Microphone] Playback started. firstBlockAfterScheduleMs="
        + ((block.DspTime - block.ScheduleDspTime) * 1000d).ToString("0.###")
        + ", frame="
        + block.Frame.ToString("0.#")
        + ", blockFrames="
        + block.Frames
        + ", blockChannels="
        + block.Channels
    );
  }

  private void SetLatency(int latencyMs)
  {
    int clampedLatency = Math.Max(
      TUFReplaySetting.MinMicrophoneOffsetMs,
      Math.Min(TUFReplaySetting.MaxMicrophoneOffsetMs, latencyMs)
    );
    _userLatencyUs = clampedLatency * 1000L;
  }

  private bool HasFailed()
  {
    if (_failed)
      return true;
    Exception failure = _renderer.Failure;
    if (failure == null)
      return false;
    Fail(failure);
    return true;
  }

  private void Fail(Exception exception)
  {
    if (_disposed)
      return;
    _failed = true;
    Unpublish();
    if (!_failureLogged)
    {
      _failureLogged = true;
      Main.Instance?.Log("[Replay/Microphone] Playback disabled. error=" + exception.Message);
    }
    LogDiagnostics();
  }

  private void LogDiagnostics()
  {
    if (_diagnosticsLogged)
      return;
    _diagnosticsLogged = true;
    ReplayMicrophoneRenderStats stats = _renderer.TakeStats();
    Main.Instance?.Log(
      "[Replay/Microphone] Playback stats. blocks="
        + stats.Blocks
        + ", missingMs="
        + (stats.MissingFrames * 1000d / _wave.SampleRate).ToString("0.###")
        + ", prefetchSeeks="
        + stats.PrefetchSeeks
        + ", jumps="
        + stats.Jumps
        + ", resyncs="
        + _resyncCount
        + ", duplicateResets="
        + _duplicateResetCount
    );
  }

  private void ResetDiagnostics()
  {
    _resyncCount = 0L;
    _duplicateResetCount = 0L;
    _scheduledLogged = false;
    _startedLogged = false;
  }

  private void DeletePlaybackFile()
  {
    try
    {
      if (!string.IsNullOrEmpty(_recording.FilePath) && File.Exists(_recording.FilePath))
        File.Delete(_recording.FilePath);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Replay/Microphone] Temp file cleanup failed. error=" + exception.Message);
    }
  }
}
