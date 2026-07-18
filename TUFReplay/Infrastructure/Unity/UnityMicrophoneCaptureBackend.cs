using System;
using System.Collections.Generic;
using System.IO;
using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;
using UnityEngine;

namespace TUFReplay.Infrastructure.Unity;

public sealed class UnityMicrophoneCaptureBackend : IMicrophoneCaptureBackend
{
  private const int SampleRate = 48000;
  private const int ClipSeconds = 10;

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
      _writer = new Pcm16WavWriter(tempPath);
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
      int frames = position >= _cursor ? position - _cursor : _clip.samples - _cursor + position;
      if (frames == 0)
        return;

      int sampleCount = checked(frames * _clip.channels);
      if (_readBuffer == null || _readBuffer.Length < sampleCount)
        _readBuffer = new float[sampleCount];
      if (!_clip.GetData(_readBuffer, _cursor))
        throw new IOException("AudioClip.GetData failed.");
      if (!_writer.TryEnqueue(_readBuffer, sampleCount, _clip.channels))
        throw new IOException("Microphone writer queue overflowed.");
      _cursor = position;
      _lastPollRealtime = now;
    }
    catch (Exception exception)
    {
      _failed = true;
      Main.Instance?.Log("[Microphone] Capture failed. error=" + exception.Message);
    }
  }

  public CapturedMicrophoneRecording EndRun()
  {
    if (_writer == null)
      return null;

    Tick();
    Pcm16WavWriter writer = _writer;
    _writer = null;
    try
    {
      long frames = writer.Complete();
      writer.Dispose();
      if (_failed || frames == 0)
      {
        DeleteTemp();
        return null;
      }

      return new CapturedMicrophoneRecording
      {
        RunId = _runId,
        TempPath = _tempPath,
        DeviceId = _deviceId,
        SampleRate = SampleRate,
        Channels = 1,
        FrameCount = frames,
        CaptureStartOffsetUs = 0,
      };
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Failed to finalize WAV. error=" + exception.Message);
      writer.Dispose();
      DeleteTemp();
      return null;
    }
    finally
    {
      _runId = null;
      _tempPath = null;
      _failed = false;
    }
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
    try
    {
      _writer.Dispose();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Writer cleanup failed. error=" + exception.Message);
    }
    _writer = null;
    DeleteTemp();
    _runId = null;
    _tempPath = null;
  }

  private void DeleteTemp()
  {
    if (string.IsNullOrEmpty(_tempPath))
      return;
    try
    {
      if (File.Exists(_tempPath))
        File.Delete(_tempPath);
    }
    catch { }
  }
}
