using System;
using System.IO;
using System.Threading;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Processing;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Transport;
using UnityEngine;

namespace TUFReplay.Microphone.Playback;

public sealed class ReplayMicrophonePlayer : IReplayMicrophonePlayer
{
  private const int DriftThresholdMilliseconds = 50;
  private readonly object _readerGate = new object();
  private readonly StoredMicrophoneRecording _recording;
  private readonly Pcm16WaveInfo _wave;
  private readonly Pcm16Limiter _limiter;
  private readonly Pcm16PrefetchBuffer _prefetch;
  private readonly GameObject _gameObject;
  private readonly AudioSource _source;
  private readonly AudioClip _clip;
  private readonly byte[] _readBuffer;
  private readonly int _frameBytes;
  private readonly int _prefillBytes;
  private readonly int _driftThresholdFrames;
  private long _readerFrame;
  private long _requestedFrame;
  private long _lastResetFrame = -1L;
  private int _seekGeneration;
  private int _activeGeneration;
  private int _lastResetGeneration = -1;
  private int _pendingRecoveryGeneration;
  private bool _preparing;
  private bool _started;
  private bool _paused;
  private bool _playbackStartLogged;
  private bool _failureLogged;
  private bool _diagnosticsLogged = true;
  private double _dspAnchorTime;
  private long _dspAnchorFrame;
  private long _microphoneLatencyUs;
  private long _underrunCount;
  private long _missingFrames;
  private long _recoverySeekCount;
  private long _duplicateResetCount;
  private long _maximumDriftFrames;
  private volatile float _gain;
  private volatile bool _failed;
  private bool _disposed;

  internal ReplayMicrophonePlayer(
    StoredMicrophoneRecording recording,
    Pcm16WaveInfo wave,
    Pcm16LimiterEnvelope limiterEnvelope,
    int userOffsetMs = 0,
    int volumeDb = 0
  )
  {
    _recording = recording ?? throw new ArgumentNullException(nameof(recording));
    _wave = wave ?? throw new ArgumentNullException(nameof(wave));
    _limiter = new Pcm16Limiter(limiterEnvelope, wave.SampleRate);
    SetLatency(userOffsetMs);
    SetVolume(volumeDb);
    if (wave.FrameCount > int.MaxValue)
      throw new InvalidDataException("The microphone recording is too long for Unity audio playback.");

    AudioSettings.GetDSPBufferSize(out int dspBufferFrames, out _);
    dspBufferFrames = Math.Max(1, dspBufferFrames);
    _frameBytes = checked(wave.Channels * 2);
    _prefillBytes = checked(dspBufferFrames * _frameBytes);
    int bufferSamples = Math.Max(8192, checked(dspBufferFrames * wave.Channels));
    _readBuffer = new byte[checked(bufferSamples * 2)];
    int prefetchFrames = Math.Max(checked(dspBufferFrames * 8), Math.Max(1, wave.SampleRate / 2));
    int prefetchBytes = Math.Max(checked(prefetchFrames * _frameBytes), checked(_readBuffer.Length * 4));
    _prefetch = new Pcm16PrefetchBuffer(
      new FileStream(recording.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read),
      wave.DataOffset,
      wave.DataLength,
      prefetchBytes
    );
    _driftThresholdFrames = Math.Max(1, wave.SampleRate * DriftThresholdMilliseconds / 1000);

    try
    {
      _gameObject = new GameObject("TUFReplay Replay Microphone");
      UnityEngine.Object.DontDestroyOnLoad(_gameObject);
      _source = _gameObject.AddComponent<AudioSource>();
      _source.playOnAwake = false;
      _source.loop = false;
      _source.spatialBlend = 0f;
      _source.volume = 1f;
      _source.pitch = 1f;
      _source.priority = 0;
      _clip = AudioClip.Create(
        "TUFReplay Replay Microphone",
        checked((int)wave.FrameCount),
        wave.Channels,
        wave.SampleRate,
        true,
        ReadSamples,
        SetReaderPosition
      );
      _source.clip = _clip;
      Main.Instance?.Log(
        "[Replay/Microphone] Player ready. frames="
          + wave.FrameCount
          + ", sampleRate="
          + wave.SampleRate
          + ", captureOffsetUs="
          + recording.CaptureStartOffsetUs
          + ", volumeDb="
          + volumeDb
      );
    }
    catch
    {
      _prefetch?.Dispose();
      if (_gameObject != null)
        UnityEngine.Object.Destroy(_gameObject);
      DeletePlaybackFile();
      throw;
    }
  }

  public void ResetTo(ReplayPlaybackSnapshot snapshot)
  {
    if (_disposed)
      return;
    if (_failed)
    {
      Fail(PrefetchFailure());
      return;
    }

    try
    {
      long targetFrame = TargetFrame(snapshot);
      if (
        ReplayMicrophonePlaybackDecisions.ShouldSuppressReset(
          targetFrame,
          _lastResetFrame,
          _seekGeneration,
          _lastResetGeneration,
          _preparing,
          _requestedFrame,
          _started || _paused,
          _source.timeSamples,
          _driftThresholdFrames
        )
      )
      {
        Interlocked.Increment(ref _duplicateResetCount);
        return;
      }

      RequestSeek(targetFrame, recovery: false);
      _lastResetFrame = targetFrame;
      _lastResetGeneration = _seekGeneration;
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void Tick(ReplayPlaybackSnapshot snapshot)
  {
    if (_disposed)
      return;
    if (_failed)
    {
      Fail(PrefetchFailure());
      return;
    }

    try
    {
      double microphoneTimeUs = ReplayMicrophoneClock.ToMicrophoneTimeUs(
        snapshot.TimelineTimeUs,
        snapshot.GameplayRate,
        EffectiveCaptureOffsetUs(),
        snapshot.WonTimeUs
      );
      long targetFrame = TargetFrame(snapshot);
      if (microphoneTimeUs < 0d)
      {
        if (_started || _paused)
          StopSource();
        return;
      }
      if (targetFrame >= _wave.FrameCount)
      {
        Stop();
        return;
      }

      int recoveryGeneration = Interlocked.Exchange(ref _pendingRecoveryGeneration, 0);
      if (ReplayMicrophonePlaybackDecisions.IsCurrentRecovery(recoveryGeneration, _activeGeneration))
        RequestSeek(targetFrame, recovery: true);

      if (snapshot.Paused)
      {
        if (_started && !_paused)
        {
          _source.Pause();
          _paused = true;
        }
        return;
      }

      if (_paused)
        RequestSeek(targetFrame, recovery: false);
      if (_preparing)
      {
        TryStartPrepared(targetFrame, snapshot.TimelineTimeUs);
        return;
      }
      if (!_started)
      {
        RequestSeek(targetFrame, recovery: false);
        TryStartPrepared(targetFrame, snapshot.TimelineTimeUs);
        return;
      }

      long expectedFrame =
        _dspAnchorFrame + (long)Math.Max(0d, (AudioSettings.dspTime - _dspAnchorTime) * _wave.SampleRate);
      long actualFrame = _source.timeSamples;
      long driftFrames = Math.Max(Math.Abs(actualFrame - expectedFrame), Math.Abs(actualFrame - targetFrame));
      RecordMaximumDrift(driftFrames);
      if (!_source.isPlaying || driftFrames >= _driftThresholdFrames)
        RequestSeek(targetFrame, recovery: true);
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
    if (_preparing || _started || _paused)
      RequestSeek(TargetFrame(snapshot), recovery: false);
  }

  public void UpdateVolume(int volumeDb)
  {
    if (!_disposed)
      SetVolume(volumeDb);
  }

  public void Stop()
  {
    if (_disposed)
      return;
    StopSource();
    LogDiagnostics();
    ResetDiagnostics();
  }

  public void Dispose()
  {
    lock (_readerGate)
    {
      if (_disposed)
        return;
      _disposed = true;
    }
    LogDiagnostics();
    try
    {
      _source.Stop();
      _source.clip = null;
    }
    catch { }
    _prefetch.Dispose();
    if (_clip != null)
      UnityEngine.Object.Destroy(_clip);
    if (_gameObject != null)
      UnityEngine.Object.Destroy(_gameObject);
    DeletePlaybackFile();
  }

  private long TargetFrame(ReplayPlaybackSnapshot snapshot) =>
    ReplayMicrophoneClock.ToFrame(snapshot, EffectiveCaptureOffsetUs(), _wave.SampleRate, _wave.FrameCount);

  private long EffectiveCaptureOffsetUs() =>
    ReplayMicrophoneClock.ApplyLatencyCorrection(_recording.CaptureStartOffsetUs, _microphoneLatencyUs);

  private void SetLatency(int latencyMs)
  {
    int clampedLatency = Math.Max(
      TUFReplaySetting.MinMicrophoneOffsetMs,
      Math.Min(TUFReplaySetting.MaxMicrophoneOffsetMs, latencyMs)
    );
    _microphoneLatencyUs = clampedLatency * 1000L;
  }

  private void SetVolume(int volumeDb)
  {
    int clampedVolumeDb = Math.Max(
      TUFReplaySetting.MinMicrophoneVolumeDb,
      Math.Min(TUFReplaySetting.MaxMicrophoneVolumeDb, volumeDb)
    );
    _gain = MicrophoneGain.FromDecibels(clampedVolumeDb);
  }

  private void RequestSeek(long frame, bool recovery)
  {
    StopSource();
    _requestedFrame = Math.Max(0L, Math.Min(frame, _wave.FrameCount));
    lock (_readerGate)
    {
      _readerFrame = _requestedFrame;
      _limiter.Reset();
    }
    _seekGeneration = _prefetch.Seek(_requestedFrame * _frameBytes);
    _activeGeneration = 0;
    _preparing = _requestedFrame < _wave.FrameCount;
    _diagnosticsLogged = false;
    Interlocked.Exchange(ref _pendingRecoveryGeneration, 0);
    if (recovery)
      Interlocked.Increment(ref _recoverySeekCount);
  }

  private void TryStartPrepared(long targetFrame, long timelineTimeUs)
  {
    if (!_preparing)
      return;
    long advanceFrames = targetFrame - _requestedFrame;
    if (advanceFrames < 0L)
    {
      RequestSeek(targetFrame, recovery: false);
      return;
    }

    long remainingFrames = _wave.FrameCount - targetFrame;
    int requiredBytes = (int)Math.Min((long)_prefillBytes, Math.Max(0L, remainingFrames * _frameBytes));
    long advanceBytesLong = advanceFrames * _frameBytes;
    if (advanceBytesLong > int.MaxValue || advanceBytesLong + requiredBytes > _prefetch.Capacity)
    {
      RequestSeek(targetFrame, recovery: false);
      return;
    }
    int advanceBytes = (int)advanceBytesLong;
    if (!_prefetch.IsReady(_seekGeneration, advanceBytes + requiredBytes))
      return;
    if (advanceBytes > 0 && !_prefetch.TrySkip(_seekGeneration, advanceBytes, _frameBytes))
      return;

    _requestedFrame = targetFrame;
    lock (_readerGate)
    {
      _readerFrame = targetFrame;
      _limiter.Reset();
    }
    _source.timeSamples = checked((int)targetFrame);
    _dspAnchorTime = AudioSettings.dspTime;
    _dspAnchorFrame = targetFrame;
    _activeGeneration = _seekGeneration;
    _preparing = false;
    _started = true;
    _paused = false;
    _source.Play();
    if (!_playbackStartLogged)
    {
      _playbackStartLogged = true;
      Main.Instance?.Log(
        "[Replay/Microphone] Playback started. replayTimeUs="
          + timelineTimeUs
          + ", targetFrame="
          + targetFrame
          + ", gain="
          + _gain
      );
    }
  }

  private void StopSource()
  {
    if (_source != null && (_started || _paused || _preparing))
      _source.Stop();
    _started = false;
    _paused = false;
    _preparing = false;
    _activeGeneration = 0;
    Interlocked.Exchange(ref _pendingRecoveryGeneration, 0);
  }

  private void ReadSamples(float[] data)
  {
    int outputOffset = 0;
    lock (_readerGate)
    {
      if (_disposed || _failed || _activeGeneration == 0)
      {
        Array.Clear(data, 0, data.Length);
        return;
      }
      if (Volatile.Read(ref _pendingRecoveryGeneration) == _activeGeneration)
      {
        Interlocked.Add(ref _missingFrames, data.Length / _wave.Channels);
        Array.Clear(data, 0, data.Length);
        return;
      }

      try
      {
        int channelCount = _wave.Channels;
        long remainingSamples = (_wave.FrameCount - _readerFrame) * channelCount;
        int requestedSamples = (int)Math.Min(data.Length, remainingSamples);
        requestedSamples -= requestedSamples % channelCount;
        while (outputOffset < requestedSamples)
        {
          int chunkSamples = Math.Min(requestedSamples - outputOffset, _readBuffer.Length / 2);
          chunkSamples -= chunkSamples % channelCount;
          if (chunkSamples <= 0)
            break;
          int bytesRead = _prefetch.Read(_readBuffer, 0, chunkSamples * 2, _frameBytes);
          int samplesRead = bytesRead / 2;
          int framesRead = samplesRead / channelCount;
          float requestedGain = _gain;
          for (int frame = 0; frame < framesRead; frame++)
          {
            float effectiveGain = _limiter.NextEffectiveGain(_readerFrame + frame, requestedGain);
            int frameSampleOffset = frame * channelCount;
            for (int channel = 0; channel < channelCount; channel++)
            {
              int sampleIndex = frameSampleOffset + channel;
              int byteIndex = sampleIndex * 2;
              short sample = (short)(_readBuffer[byteIndex] | (_readBuffer[byteIndex + 1] << 8));
              data[outputOffset + sampleIndex] = Mathf.Clamp(sample / 32768f * effectiveGain, -1f, 1f);
            }
          }
          outputOffset += samplesRead;
          _readerFrame += framesRead;
          if (samplesRead < chunkSamples)
          {
            int missingFrames = (requestedSamples - outputOffset) / channelCount;
            _readerFrame += missingFrames;
            Interlocked.Increment(ref _underrunCount);
            Interlocked.Add(ref _missingFrames, missingFrames);
            Interlocked.CompareExchange(ref _pendingRecoveryGeneration, _activeGeneration, 0);
            break;
          }
        }
        if (_prefetch.Failure != null)
          throw _prefetch.Failure;
      }
      catch
      {
        _failed = true;
      }
      if (outputOffset < data.Length)
        Array.Clear(data, outputOffset, data.Length - outputOffset);
    }
  }

  private void SetReaderPosition(int frame)
  {
    lock (_readerGate)
    {
      if (_disposed)
        return;
      long clampedFrame = Math.Max(0, Math.Min(frame, checked((int)_wave.FrameCount)));
      if (clampedFrame == _readerFrame)
        return;
      _readerFrame = clampedFrame;
      _limiter.Reset();
      int generation = _activeGeneration;
      if (generation != 0)
        Interlocked.CompareExchange(ref _pendingRecoveryGeneration, generation, 0);
    }
  }

  private void RecordMaximumDrift(long driftFrames)
  {
    long observed = Interlocked.Read(ref _maximumDriftFrames);
    while (driftFrames > observed)
    {
      long previous = Interlocked.CompareExchange(ref _maximumDriftFrames, driftFrames, observed);
      if (previous == observed)
        return;
      observed = previous;
    }
  }

  private Exception PrefetchFailure() =>
    _prefetch.Failure ?? new InvalidOperationException("The microphone audio callback failed.");

  private void Fail(Exception exception)
  {
    if (_disposed)
      return;
    _failed = true;
    StopSource();
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
    double maximumDriftMs = Interlocked.Read(ref _maximumDriftFrames) * 1000d / _wave.SampleRate;
    Main.Instance?.Log(
      "[Replay/Microphone] Playback stats. underruns="
        + Interlocked.Read(ref _underrunCount)
        + ", missingFrames="
        + Interlocked.Read(ref _missingFrames)
        + ", recoverySeeks="
        + Interlocked.Read(ref _recoverySeekCount)
        + ", duplicateResets="
        + Interlocked.Read(ref _duplicateResetCount)
        + ", maxDriftMs="
        + maximumDriftMs.ToString("0.###")
    );
  }

  private void ResetDiagnostics()
  {
    Interlocked.Exchange(ref _underrunCount, 0L);
    Interlocked.Exchange(ref _missingFrames, 0L);
    Interlocked.Exchange(ref _recoverySeekCount, 0L);
    Interlocked.Exchange(ref _duplicateResetCount, 0L);
    Interlocked.Exchange(ref _maximumDriftFrames, 0L);
    _playbackStartLogged = false;
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
