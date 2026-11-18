using System;
using System.IO;
using System.Threading;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Processing;
using TUFReplay.Replay.Playback;
using UnityEngine;

namespace TUFReplay.Microphone.Playback;

// Plays a run's microphone recording during an in-game replay. Chunks of the
// recording are written into in-memory clips on the main thread and scheduled
// on the audio clock, about two seconds ahead. No code of ours runs on the
// audio thread: with a managed audio callback, a full garbage collection can
// stall the mixer, and the output then repeats its last few milliseconds as a
// loud tone.
public sealed class ReplayMicrophonePlayer : IReplayMicrophonePlayer
{
  // A playing schedule moves once the replay timeline maps this far from it,
  // for example across a pause section of the recording.
  private const int ResyncThresholdMilliseconds = 30;

  // A reset this close to the playing schedule keeps it, so repeated resets
  // of the same position do not click.
  private const int DuplicateResetMilliseconds = 2;

  // The prefetch starts this far before a new run's first frame, in case that
  // run starts a little earlier than planned.
  private const int PrimeLeadMilliseconds = 20;

  private const int ChunkMilliseconds = 500;
  private const int VoiceCount = 4;

  // A mono recording plays on both speakers at full level, the same as stereo.
  private const int OutputChannels = 2;

  // A new run starts this far ahead, so it is scheduled before its start time.
  private const double MinimumStartLeadSeconds = 0.06d;
  private const int StartLeadDspBuffers = 4;

  private readonly StoredMicrophoneRecording _recording;
  private readonly Pcm16WaveInfo _wave;
  private readonly ReplayMicrophoneRenderer _renderer;
  private readonly ReplayMicrophoneChunkQueue _queue;
  private readonly GameObject _gameObject;
  private readonly ReplayMicrophoneQueueDriver _driver;
  private readonly ReplayMicrophoneVoice[] _voices = new ReplayMicrophoneVoice[VoiceCount];
  private readonly int _resyncThresholdFrames;
  private readonly int _duplicateResetFrames;
  private readonly int _primeLeadFrames;
  private readonly int _startLeadFrames;
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
    int outputSampleRate = Math.Max(1, AudioSettings.outputSampleRate);
    AudioSettings.GetDSPBufferSize(out int dspBufferFrames, out _);
    dspBufferFrames = Math.Max(1, dspBufferFrames);
    int chunkFrames = Math.Max(1, outputSampleRate * ChunkMilliseconds / 1000);
    double startLeadSeconds = Math.Max(
      MinimumStartLeadSeconds,
      StartLeadDspBuffers * dspBufferFrames / (double)outputSampleRate
    );
    _resyncThresholdFrames = Math.Max(1, wave.SampleRate * ResyncThresholdMilliseconds / 1000);
    _duplicateResetFrames = Math.Max(1, wave.SampleRate * DuplicateResetMilliseconds / 1000);
    _primeLeadFrames = Math.Max(0, wave.SampleRate * PrimeLeadMilliseconds / 1000);
    _startLeadFrames = (int)Math.Ceiling(wave.SampleRate * startLeadSeconds);

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
      outputSampleRate,
      chunkFrames
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
      for (int i = 0; i < _voices.Length; i++)
        _voices[i] = new ReplayMicrophoneVoice(_gameObject, chunkFrames, OutputChannels, outputSampleRate);
      _queue = new ReplayMicrophoneChunkQueue(
        _renderer,
        _voices,
        chunkFrames,
        OutputChannels,
        outputSampleRate,
        startLeadSeconds
      );
      _driver = _gameObject.AddComponent<ReplayMicrophoneQueueDriver>();
      _driver.Player = this;

      Main.Instance?.Log(
        "[Replay/Microphone] Player ready. frames="
          + wave.FrameCount
          + ", sampleRate="
          + wave.SampleRate
          + ", outputSampleRate="
          + outputSampleRate
          + ", dspBufferFrames="
          + dspBufferFrames
          + ", chunkFrames="
          + chunkFrames
          + ", startLeadMs="
          + (startLeadSeconds * 1000d).ToString("0.#")
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
      DisposeVoices();
      if (_gameObject != null)
        UnityEngine.Object.Destroy(_gameObject);
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
    // The volume is part of the samples, so queued chunks are rendered again.
    if (_schedule != null)
      _queue.Restart();
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
      _driver.Player = null;
      DisposeVoices();
    }
    catch { }
    if (_gameObject != null)
      UnityEngine.Object.Destroy(_gameObject);
    ThreadPool.QueueUserWorkItem(_ =>
    {
      // Stopping the disk reader can wait for a read, away from the game's frame.
      _renderer.Dispose();
      DeletePlaybackFile();
    });
  }

  // Called every frame by the driver, and when a schedule is published.
  internal void Pump()
  {
    if (_disposed || _schedule == null || HasFailed())
      return;

    try
    {
      PumpQueue(AudioSettings.dspTime);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  private void PumpQueue(double now)
  {
    // Paused sources do not follow the audio clock; start again after the pause.
    if (AudioListener.pause)
    {
      _queue.Stop(now);
      return;
    }
    _queue.Pump(now);
    LogStart(now);
  }

  private void DisposeVoices()
  {
    foreach (ReplayMicrophoneVoice voice in _voices)
      voice?.Dispose();
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
    _renderer.Prime((long)Math.Floor(frame) + _startLeadFrames - _primeLeadFrames);
    _renderer.Publish(_schedule);
    _queue.Restart();
    if (!_startedLogged)
      _queue.WatchNextStart();
    PumpQueue(now);
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
    _queue.Stop(AudioSettings.dspTime);
  }

  // How far the first chunk's position is from the audio clock once it plays.
  private void LogStart(double now)
  {
    if (_startedLogged || !_queue.TryMeasureStart(now, out double errorSeconds))
      return;
    _startedLogged = true;
    Main.Instance?.Log(
      "[Replay/Microphone] Playback started. startErrorMs=" + (errorSeconds * 1000d).ToString("0.###")
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
    try
    {
      Unpublish();
    }
    catch { }
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
    ReplayMicrophoneRenderStats renderStats = _renderer.TakeStats();
    ReplayMicrophoneQueueStats queueStats = _queue.TakeStats();
    Main.Instance?.Log(
      "[Replay/Microphone] Playback stats. chunks="
        + queueStats.Chunks
        + ", underruns="
        + queueStats.Underruns
        + ", lostVoices="
        + queueStats.LostVoices
        + ", missingMs="
        + (renderStats.MissingFrames * 1000d / _wave.SampleRate).ToString("0.###")
        + ", prefetchSeeks="
        + renderStats.PrefetchSeeks
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
