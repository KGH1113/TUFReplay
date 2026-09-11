using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TUFReplay.Microphone.Capture;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Processing;
using UnityEngine;

namespace TUFReplay.Microphone.Capture;

public sealed class UnityMicrophoneCaptureBackend : IMicrophoneCaptureBackend
{
  private const int SampleRate = 48000;
  private const int ClipSeconds = 10;
  internal const int CaptureChunkFrames = SampleRate / 4;
  internal const int WriterQueueCapacity = ClipSeconds * SampleRate / CaptureChunkFrames + 1;

  private AudioClip _clip;
  private string _deviceId;
  private int _cursor;
  private float[] _readBuffer;
  private Pcm16WavWriter _writer;
  private string _runId;
  private string _tempPath;
  private bool _failed;
  private float _lastPollRealtime;

  public void RequestPermission() { }

  public void RefreshPermissionStatus() { }

  public MicrophonePermissionStatus GetPermissionStatus() =>
    new MicrophonePermissionStatus { State = MicrophonePermissionState.NotApplicable };

  public List<MicrophoneDeviceInfo> ListDevices() => UnityMicrophoneDeviceProvider.ListDevices();

  public bool Arm(string deviceId, out string error)
  {
    Disarm();
    error = null;
    try
    {
      _deviceId = deviceId;
      _clip = UnityEngine.Microphone.Start(deviceId, true, ClipSeconds, SampleRate);
      if (_clip == null)
      {
        error = "Unity Microphone.Start returned no AudioClip.";
        return false;
      }

      return true;
    }
    catch (Exception exception)
    {
      error = exception.Message;
      Disarm();
      return false;
    }
  }

  public MicrophoneArmStatus GetArmStatus() =>
    new MicrophoneArmStatus
    {
      State = _clip == null ? MicrophoneArmState.Idle : MicrophoneArmState.Armed,
      Error = null,
    };

  public bool BeginRun(string runId, string tempPath, out string error)
  {
    error = null;
    if (_clip == null)
    {
      error = "Microphone is not armed.";
      return false;
    }

    AbortWriter();
    try
    {
      int position = UnityEngine.Microphone.GetPosition(_deviceId);
      if (position < 0)
      {
        error = "Microphone sample position is unavailable.";
        return false;
      }

      _cursor = position;
      _runId = runId;
      _tempPath = tempPath;
      _failed = false;
      _lastPollRealtime = Time.realtimeSinceStartup;
      _writer = new Pcm16WavWriter(tempPath, WriterQueueCapacity);
      return true;
    }
    catch (Exception exception)
    {
      error = exception.Message;
      AbortWriter();
      return false;
    }
  }

  public void Tick()
  {
    DrainAvailable(includePartialChunk: false);
  }

  private void DrainAvailable(bool includePartialChunk)
  {
    if (_writer == null || _failed || _clip == null)
      return;

    try
    {
      float now = Time.realtimeSinceStartup;
      if (now - _lastPollRealtime >= ClipSeconds)
        throw new IOException("Microphone polling stalled longer than the loop buffer.");
      int position = UnityEngine.Microphone.GetPosition(_deviceId);
      if (position < 0)
        throw new IOException("Microphone device became unavailable.");
      int availableFrames = MicrophoneCaptureChunking.AvailableFrames(_cursor, position, _clip.samples);
      if (availableFrames == 0)
        return;

      while (availableFrames > 0)
      {
        int frames = MicrophoneCaptureChunking.NextChunkFrames(
          availableFrames,
          CaptureChunkFrames,
          includePartialChunk
        );
        if (frames == 0)
          break;

        int sampleCount = checked(frames * _clip.channels);
        float[] buffer;
        if (frames == CaptureChunkFrames)
        {
          // AudioClip.GetData reads buffer.Length samples, so this reusable buffer must stay exact.
          if (_readBuffer == null || _readBuffer.Length != sampleCount)
            _readBuffer = new float[sampleCount];
          buffer = _readBuffer;
        }
        else
        {
          buffer = new float[sampleCount];
        }

        if (!_clip.GetData(buffer, _cursor))
          throw new IOException("AudioClip.GetData failed.");
        if (!_writer.TryEnqueue(buffer, sampleCount, _clip.channels))
          throw new IOException("Microphone writer queue overflowed.");

        _cursor = MicrophoneCaptureChunking.AdvanceCursor(_cursor, frames, _clip.samples);
        availableFrames -= frames;
        _lastPollRealtime = now;
      }
    }
    catch (Exception exception)
    {
      _failed = true;
      Main.Instance?.Log("[Microphone] Capture failed. error=" + exception.Message);
    }
  }

  public Task<CapturedMicrophoneRecording> EndRunAsync()
  {
    if (_writer == null)
      return Task.FromResult<CapturedMicrophoneRecording>(null);

    DrainAvailable(includePartialChunk: true);
    Pcm16WavWriter writer = _writer;
    string runId = _runId;
    string tempPath = _tempPath;
    string deviceId = _deviceId;
    bool failed = _failed;
    _writer = null;
    _runId = null;
    _tempPath = null;
    _failed = false;

    return Task.Run(() =>
    {
      try
      {
        long frames = writer.Complete();
        writer.Dispose();
        if (failed || frames == 0)
        {
          DeleteTemp(tempPath);
          return null;
        }

        return new CapturedMicrophoneRecording
        {
          RunId = runId,
          TempPath = tempPath,
          DeviceId = deviceId,
          SampleRate = SampleRate,
          Channels = 1,
          FrameCount = frames,
          CaptureStartOffsetUs = 0,
        };
      }
      catch (Exception exception)
      {
        Main.Instance?.Log("[Microphone] Failed to finalize WAV. error=" + exception.Message);
        try
        {
          writer.Dispose();
        }
        catch { }
        DeleteTemp(tempPath);
        return null;
      }
    });
  }

  public void Disarm()
  {
    AbortWriter();
    if (_clip != null)
    {
      try
      {
        UnityEngine.Microphone.End(_deviceId);
      }
      catch (Exception exception)
      {
        Main.Instance?.Log("[Microphone] Disarm failed. error=" + exception.Message);
      }
    }

    _clip = null;
    _deviceId = null;
    _readBuffer = null;
  }

  public void Dispose() => Disarm();

  private void AbortWriter()
  {
    if (_writer == null)
      return;
    Pcm16WavWriter writer = _writer;
    string tempPath = _tempPath;
    _writer = null;
    _runId = null;
    _tempPath = null;
    Task.Run(() =>
    {
      try
      {
        writer.Dispose();
      }
      catch (Exception exception)
      {
        Main.Instance?.Log("[Microphone] Writer cleanup failed. error=" + exception.Message);
      }
      DeleteTemp(tempPath);
    });
  }

  private static void DeleteTemp(string path)
  {
    if (string.IsNullOrEmpty(path))
      return;
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch { }
  }
}
