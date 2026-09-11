using System;
using System.IO;
using System.Threading;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Processing;

internal sealed class Pcm16LimiterEnvelope
{
  internal const float Ceiling = 0.9660509f;

  private readonly float[] _controlPeaks;
  private readonly int _sampleRate;

  internal Pcm16LimiterEnvelope(float[] controlPeaks, int sampleRate)
  {
    _controlPeaks = controlPeaks ?? throw new ArgumentNullException(nameof(controlPeaks));
    _sampleRate = sampleRate > 0 ? sampleRate : throw new ArgumentOutOfRangeException(nameof(sampleRate));
  }

  internal int BinCount => _controlPeaks.Length;

  internal float ControlPeakAt(long frame)
  {
    if (_controlPeaks.Length == 0)
      return 0f;

    long clampedFrame = Math.Max(0L, frame);
    long wholeSeconds = clampedFrame / _sampleRate;
    long remainderFrames = clampedFrame % _sampleRate;
    long binLong = wholeSeconds * 1000L + remainderFrames * 1000L / _sampleRate;
    int bin = (int)Math.Min(binLong, _controlPeaks.Length - 1L);
    float current = _controlPeaks[bin];
    if (bin + 1 >= _controlPeaks.Length || _controlPeaks[bin + 1] <= current)
      return current;

    long binStartFrame = BinStartFrame(bin, _sampleRate);
    long binEndFrame = BinStartFrame(bin + 1, _sampleRate);
    long binFrames = Math.Max(1L, binEndFrame - binStartFrame);
    float progress = (float)Math.Min(binFrames, clampedFrame - binStartFrame) / binFrames;
    return current + (_controlPeaks[bin + 1] - current) * progress;
  }

  internal float RequiredLimiterGain(long frame, float requestedGain)
  {
    float amplifiedPeak = ControlPeakAt(frame) * Math.Max(0f, requestedGain);
    return amplifiedPeak <= Ceiling ? 1f : Ceiling / amplifiedPeak;
  }

  private static long BinStartFrame(int bin, int sampleRate) => ((long)bin * sampleRate + 999L) / 1000L;
}

internal sealed class Pcm16Limiter
{
  private const float ReleaseSeconds = 0.03f;

  private readonly Pcm16LimiterEnvelope _envelope;
  private readonly float _releaseStep;
  private float _limiterGain = 1f;

  internal Pcm16Limiter(Pcm16LimiterEnvelope envelope, int sampleRate)
  {
    _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    if (sampleRate <= 0)
      throw new ArgumentOutOfRangeException(nameof(sampleRate));
    _releaseStep = 1f - (float)Math.Exp(-1d / (sampleRate * ReleaseSeconds));
  }

  internal float NextEffectiveGain(long frame, float requestedGain)
  {
    float safeRequestedGain = Math.Max(0f, requestedGain);
    float requiredLimiterGain = _envelope.RequiredLimiterGain(frame, safeRequestedGain);
    if (requiredLimiterGain < _limiterGain)
      _limiterGain = requiredLimiterGain;
    else
      _limiterGain += (1f - _limiterGain) * _releaseStep;
    return safeRequestedGain * _limiterGain;
  }

  internal void Reset() => _limiterGain = 1f;
}

internal static class Pcm16WaveAnalyzer
{
  private const int OversampleFactor = 4;
  private const int LookAheadMilliseconds = 5;
  private const int ReadBufferBytes = 64 * 1024;

  internal static Pcm16LimiterEnvelope Analyze(
    StoredMicrophoneRecording recording,
    Pcm16WaveInfo wave,
    CancellationToken cancellationToken
  )
  {
    if (recording == null)
      throw new ArgumentNullException(nameof(recording));
    if (wave == null)
      throw new ArgumentNullException(nameof(wave));
    cancellationToken.ThrowIfCancellationRequested();

    int binCount = checked((int)(FrameToBin(wave.FrameCount - 1L, wave.SampleRate) + 1L));
    var peaks = new float[binCount];
    using var stream = new FileStream(recording.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    stream.Position = wave.DataOffset;
    var reader = new Pcm16FrameReader(stream, wave.Channels, wave.DataLength, ReadBufferBytes);

    float[] x0 = new float[wave.Channels];
    float[] x1 = new float[wave.Channels];
    float[] x2 = new float[wave.Channels];
    float[] x3 = new float[wave.Channels];
    reader.ReadFrameRequired(x1);
    Array.Copy(x1, x0, wave.Channels);
    if (!reader.TryReadFrame(x2))
      Array.Copy(x1, x2, wave.Channels);
    if (!reader.TryReadFrame(x3))
      Array.Copy(x2, x3, wave.Channels);

    long nextFrameToRead = Math.Min(3L, wave.FrameCount);
    for (long frame = 0; frame < wave.FrameCount; frame++)
    {
      if ((frame & 4095L) == 0L)
        cancellationToken.ThrowIfCancellationRequested();

      float segmentPeak = SegmentTruePeak(x0, x1, x2, x3);
      int bin = checked((int)FrameToBin(frame, wave.SampleRate));
      peaks[bin] = Math.Max(peaks[bin], segmentPeak);
      if (frame + 1L < wave.FrameCount)
      {
        int nextBin = checked((int)FrameToBin(frame + 1L, wave.SampleRate));
        peaks[nextBin] = Math.Max(peaks[nextBin], segmentPeak);
      }

      if (frame + 1L >= wave.FrameCount)
        break;

      float[] spare = x0;
      x0 = x1;
      x1 = x2;
      x2 = x3;
      x3 = spare;
      if (nextFrameToRead < wave.FrameCount)
      {
        reader.ReadFrameRequired(x3);
        nextFrameToRead++;
      }
      else
      {
        Array.Copy(x2, x3, wave.Channels);
      }
    }

    cancellationToken.ThrowIfCancellationRequested();
    return new Pcm16LimiterEnvelope(BuildLookAheadControlPeaks(peaks), wave.SampleRate);
  }

  private static float[] BuildLookAheadControlPeaks(float[] peaks)
  {
    var control = (float[])peaks.Clone();
    for (int bin = 0; bin < peaks.Length; bin++)
    {
      int futureCount = Math.Min(LookAheadMilliseconds, peaks.Length - bin - 1);
      for (int distance = 1; distance <= futureCount; distance++)
      {
        float anticipation = 1f - distance / (float)LookAheadMilliseconds;
        control[bin] = Math.Max(control[bin], peaks[bin + distance] * anticipation);
      }
    }
    return control;
  }

  private static float SegmentTruePeak(float[] x0, float[] x1, float[] x2, float[] x3)
  {
    float peak = 0f;
    for (int channel = 0; channel < x1.Length; channel++)
    {
      float a0 = -0.5f * x0[channel] + 1.5f * x1[channel] - 1.5f * x2[channel] + 0.5f * x3[channel];
      float a1 = x0[channel] - 2.5f * x1[channel] + 2f * x2[channel] - 0.5f * x3[channel];
      float a2 = -0.5f * x0[channel] + 0.5f * x2[channel];
      float a3 = x1[channel];
      for (int phase = 0; phase < OversampleFactor; phase++)
      {
        float t = phase / (float)OversampleFactor;
        float sample = ((a0 * t + a1) * t + a2) * t + a3;
        peak = Math.Max(peak, Math.Abs(sample));
      }
    }
    return peak;
  }

  private static long FrameToBin(long frame, int sampleRate)
  {
    long wholeSeconds = frame / sampleRate;
    return wholeSeconds * 1000L + frame % sampleRate * 1000L / sampleRate;
  }

  private sealed class Pcm16FrameReader
  {
    private readonly Stream _stream;
    private readonly int _channels;
    private readonly byte[] _buffer;
    private long _remainingBytes;
    private int _offset;
    private int _count;

    internal Pcm16FrameReader(Stream stream, int channels, long dataLength, int bufferBytes)
    {
      _stream = stream;
      _channels = channels;
      _remainingBytes = dataLength;
      _buffer = new byte[Math.Max(channels * 2, bufferBytes)];
    }

    internal void ReadFrameRequired(float[] destination)
    {
      if (!TryReadFrame(destination))
        throw new EndOfStreamException("The microphone WAV sample data is truncated.");
    }

    internal bool TryReadFrame(float[] destination)
    {
      int frameBytes = _channels * 2;
      if (_remainingBytes < frameBytes)
        return false;

      for (int channel = 0; channel < _channels; channel++)
      {
        int low = ReadByteRequired();
        int high = ReadByteRequired();
        destination[channel] = (short)(low | (high << 8)) / 32768f;
      }
      return true;
    }

    private int ReadByteRequired()
    {
      if (_offset >= _count)
      {
        _count = _stream.Read(_buffer, 0, (int)Math.Min(_buffer.Length, _remainingBytes));
        _offset = 0;
        if (_count <= 0)
          throw new EndOfStreamException("The microphone WAV sample data is truncated.");
      }
      _remainingBytes--;
      return _buffer[_offset++];
    }
  }
}
