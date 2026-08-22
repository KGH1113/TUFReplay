using System;
using System.IO;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Playback;
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
  private readonly int _driftThresholdFrames;
  private long _readerFrame;
  private bool _started;
  private bool _paused;
  private long _microphoneLatencyUs;
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
    int bufferSamples = Math.Max(8192, checked(Math.Max(1, dspBufferFrames) * wave.Channels));
    _readBuffer = new byte[checked(bufferSamples * 2)];
    int prefetchFrames = Math.Max(checked(Math.Max(1, dspBufferFrames) * 8), Math.Max(1, wave.SampleRate / 2));
    int prefetchBytes = Math.Max(checked(prefetchFrames * wave.Channels * 2), checked(_readBuffer.Length * 4));
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
      SetReaderPosition(0);
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

  public void ResetTo(long replayTimeUs, double gameplayRate, long? wonTimeUs)
  {
    if (_disposed)
      return;
    if (_failed)
    {
      Stop();
      return;
    }

    try
    {
      _source.Stop();
      _started = false;
      _paused = false;
      SetPlaybackPosition(TargetFrame(replayTimeUs, gameplayRate, wonTimeUs));
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void Tick(long replayTimeUs, double gameplayRate, long? wonTimeUs, bool paused)
  {
    if (_disposed)
      return;
    if (_failed)
    {
      Stop();
      return;
    }

    try
    {
      double microphoneTimeUs = ReplayMicrophoneClock.ToMicrophoneTimeUs(
        replayTimeUs,
        gameplayRate,
        EffectiveCaptureOffsetUs(),
        wonTimeUs
      );
      if (microphoneTimeUs < 0d)
      {
        if (_started)
          ResetTo(replayTimeUs, gameplayRate, wonTimeUs);
        return;
      }

      long targetFrame = TargetFrame(replayTimeUs, gameplayRate, wonTimeUs);
      if (targetFrame >= _wave.FrameCount)
      {
        Stop();
        return;
      }

      if (paused)
      {
        if (_started && !_paused)
        {
          _source.Pause();
          _paused = true;
        }
        return;
      }

      if (!_started)
      {
        SetPlaybackPosition(targetFrame);
        _source.Play();
        _started = true;
        _paused = false;
        Main.Instance?.Log(
          "[Replay/Microphone] Playback started. replayTimeUs="
            + replayTimeUs
            + ", targetFrame="
            + targetFrame
            + ", gain="
            + _gain
        );
        return;
      }

      if (_paused)
      {
        SetPlaybackPosition(targetFrame);
        _source.UnPause();
        _paused = false;
        return;
      }

      if (!_source.isPlaying || Math.Abs((long)_source.timeSamples - targetFrame) >= _driftThresholdFrames)
      {
        SetPlaybackPosition(targetFrame);
        if (!_source.isPlaying)
          _source.Play();
      }
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void UpdateLatency(int latencyMs, long replayTimeUs, double gameplayRate, long? wonTimeUs)
  {
    if (_disposed)
      return;
    SetLatency(latencyMs);
    if (_started || _paused)
      SetPlaybackPosition(TargetFrame(replayTimeUs, gameplayRate, wonTimeUs));
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
    _source.Stop();
    _started = false;
    _paused = false;
  }

  public void Dispose()
  {
    lock (_readerGate)
    {
      if (_disposed)
        return;
      _disposed = true;
    }

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

  private long TargetFrame(long replayTimeUs, double gameplayRate, long? wonTimeUs)
  {
    return ReplayMicrophoneClock.ToFrame(
      replayTimeUs,
      gameplayRate,
      EffectiveCaptureOffsetUs(),
      _wave.SampleRate,
      _wave.FrameCount,
      wonTimeUs
    );
  }

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

  private void SetPlaybackPosition(long frame)
  {
    int position = checked((int)Math.Max(0L, Math.Min(frame, _wave.FrameCount - 1L)));
    _source.timeSamples = position;
    SetReaderPosition(position);
  }

  private void ReadSamples(float[] data)
  {
    int outputOffset = 0;
    lock (_readerGate)
    {
      if (_disposed || _failed)
      {
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

          int requestedBytes = chunkSamples * 2;
          int bytesRead = _prefetch.Read(_readBuffer, 0, requestedBytes, channelCount * 2);
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
            int skippedFrames = (chunkSamples - samplesRead) / channelCount;
            _readerFrame += skippedFrames;
            _limiter.Reset();
            _prefetch.Seek(_readerFrame * channelCount * 2L);
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
      _readerFrame = Math.Max(0, Math.Min(frame, checked((int)_wave.FrameCount)));
      _limiter.Reset();
      _prefetch.Seek(_readerFrame * _wave.Channels * 2L);
    }
  }

  private void Fail(Exception exception)
  {
    _failed = true;
    try
    {
      _source.Stop();
    }
    catch { }
    Main.Instance?.Log("[Replay/Microphone] Playback disabled. error=" + exception.Message);
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
