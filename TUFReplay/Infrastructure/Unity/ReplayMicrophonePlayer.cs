using System;
using System.IO;
using TUFReplay.Application.Microphone;
using TUFReplay.Application.Replay;
using TUFReplay.Domain.Microphone;
using UnityEngine;

namespace TUFReplay.Infrastructure.Unity;

public sealed class ReplayMicrophonePlayer : IReplayMicrophonePlayer
{
  private const int DriftThresholdMilliseconds = 50;

  private readonly object _streamGate = new object();
  private readonly StoredMicrophoneRecording _recording;
  private readonly Pcm16WaveInfo _wave;
  private readonly FileStream _stream;
  private readonly GameObject _gameObject;
  private readonly AudioSource _source;
  private readonly AudioClip _clip;
  private readonly byte[] _readBuffer;
  private readonly int _driftThresholdFrames;
  private long _readerFrame;
  private bool _started;
  private bool _paused;
  private volatile bool _failed;
  private bool _disposed;

  public ReplayMicrophonePlayer(StoredMicrophoneRecording recording, Pcm16WaveInfo wave)
  {
    _recording = recording ?? throw new ArgumentNullException(nameof(recording));
    _wave = wave ?? throw new ArgumentNullException(nameof(wave));
    if (wave.FrameCount > int.MaxValue)
      throw new InvalidDataException("The microphone recording is too long for Unity audio playback.");

    _stream = new FileStream(recording.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    AudioSettings.GetDSPBufferSize(out int dspBufferFrames, out _);
    int bufferSamples = Math.Max(8192, checked(Math.Max(1, dspBufferFrames) * wave.Channels));
    _readBuffer = new byte[checked(bufferSamples * 2)];
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
    }
    catch
    {
      _stream.Dispose();
      if (_gameObject != null)
        UnityEngine.Object.Destroy(_gameObject);
      DeletePlaybackFile();
      throw;
    }
  }

  public void ResetTo(long replayTimeUs, double timelineRate)
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
      SetPlaybackPosition(TargetFrame(replayTimeUs, timelineRate));
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void Tick(long replayTimeUs, double timelineRate, bool paused)
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
        timelineRate,
        _recording.CaptureStartOffsetUs
      );
      if (microphoneTimeUs < 0d)
      {
        if (_started)
          ResetTo(replayTimeUs, timelineRate);
        return;
      }

      long targetFrame = TargetFrame(replayTimeUs, timelineRate);
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
    if (_disposed)
      return;
    _disposed = true;

    try
    {
      _source.Stop();
      _source.clip = null;
    }
    catch { }

    lock (_streamGate)
      _stream.Dispose();
    if (_clip != null)
      UnityEngine.Object.Destroy(_clip);
    if (_gameObject != null)
      UnityEngine.Object.Destroy(_gameObject);
    DeletePlaybackFile();
  }

  private long TargetFrame(long replayTimeUs, double timelineRate)
  {
    return ReplayMicrophoneClock.ToFrame(
      replayTimeUs,
      timelineRate,
      _recording.CaptureStartOffsetUs,
      _wave.SampleRate,
      _wave.FrameCount
    );
  }

  private void SetPlaybackPosition(long frame)
  {
    int position = checked((int)Math.Max(0L, Math.Min(frame, _wave.FrameCount - 1L)));
    _source.timeSamples = position;
    SetReaderPosition(position);
  }

  private void ReadSamples(float[] data)
  {
    Array.Clear(data, 0, data.Length);
    lock (_streamGate)
    {
      if (_disposed || _failed)
        return;

      try
      {
        int channelCount = _wave.Channels;
        long remainingSamples = (_wave.FrameCount - _readerFrame) * channelCount;
        int requestedSamples = (int)Math.Min(data.Length, remainingSamples);
        requestedSamples -= requestedSamples % channelCount;
        int outputOffset = 0;

        while (outputOffset < requestedSamples)
        {
          int chunkSamples = Math.Min(requestedSamples - outputOffset, _readBuffer.Length / 2);
          chunkSamples -= chunkSamples % channelCount;
          if (chunkSamples <= 0)
            break;

          int requestedBytes = chunkSamples * 2;
          int bytesRead = ReadFully(_readBuffer, requestedBytes);
          int samplesRead = bytesRead / 2;
          samplesRead -= samplesRead % channelCount;
          for (int index = 0; index < samplesRead; index++)
          {
            int byteIndex = index * 2;
            short sample = (short)(_readBuffer[byteIndex] | (_readBuffer[byteIndex + 1] << 8));
            data[outputOffset + index] = sample / 32768f;
          }

          outputOffset += samplesRead;
          _readerFrame += samplesRead / channelCount;
          if (samplesRead < chunkSamples)
            break;
        }
      }
      catch
      {
        _failed = true;
      }
    }
  }

  private int ReadFully(byte[] buffer, int count)
  {
    int total = 0;
    while (total < count)
    {
      int read = _stream.Read(buffer, total, count - total);
      if (read <= 0)
        break;
      total += read;
    }
    return total;
  }

  private void SetReaderPosition(int frame)
  {
    lock (_streamGate)
    {
      if (_disposed)
        return;
      _readerFrame = Math.Max(0, Math.Min(frame, checked((int)_wave.FrameCount)));
      _stream.Position = _wave.DataOffset + _readerFrame * _wave.Channels * 2L;
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
