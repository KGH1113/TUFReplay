using System;
using System.Threading;
using TUFReplay.Application.Calibration;
using UnityEngine;

namespace TUFReplay.Features.Calibration;

public sealed class CalibrationGameAudioTap : MonoBehaviour
{
  private const int MaximumChunks = 4096;
  private readonly float[] _peaks = new float[MaximumChunks];
  private readonly long[] _endFrames = new long[MaximumChunks];
  private int _count;
  private long _frames;
  private int _sampleRate;
  private volatile bool _capturing;
  private volatile bool _overflowed;

  public void BeginCapture()
  {
    _sampleRate = AudioSettings.outputSampleRate;
    _count = 0;
    _frames = 0L;
    _overflowed = false;
    _capturing = true;
  }

  public float[] Finish(double durationMs)
  {
    _capturing = false;
    Thread.MemoryBarrier();
    if (_overflowed)
      throw new InvalidOperationException("The calibration game audio waveform buffer overflowed.");
    int count = Volatile.Read(ref _count);
    return CalibrationWaveformBuilder.FromTimedPeaks(_peaks, _endFrames, count, _sampleRate, durationMs);
  }

  private void OnAudioFilterRead(float[] data, int channels)
  {
    if (!_capturing || data == null || channels <= 0)
      return;
    int index = Volatile.Read(ref _count);
    if (index >= MaximumChunks)
    {
      _overflowed = true;
      return;
    }

    float peak = 0f;
    for (int i = 0; i < data.Length; i++)
      peak = Math.Max(peak, Math.Abs(data[i]));
    long frames = _frames + data.Length / channels;
    _peaks[index] = peak;
    _endFrames[index] = frames;
    _frames = frames;
    Volatile.Write(ref _count, index + 1);
  }
}
