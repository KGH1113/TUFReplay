using System;
using System.IO;
using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Application.Calibration;

public static class CalibrationWaveformBuilder
{
  public const int BinCount = 480;

  public static float[] FromPcm16(CapturedMicrophoneRecording recording, double durationMs)
  {
    if (recording == null)
      throw new ArgumentNullException(nameof(recording));
    var stored = new StoredMicrophoneRecording
    {
      RunId = recording.RunId,
      FilePath = recording.TempPath,
      Format = "wav/pcm16",
      SampleRate = recording.SampleRate,
      Channels = recording.Channels,
      FrameCount = recording.FrameCount,
      CaptureStartOffsetUs = recording.CaptureStartOffsetUs,
      ByteLength = new FileInfo(recording.TempPath).Length,
    };
    Pcm16WaveInfo wave = Pcm16WaveFile.ReadAndValidate(stored);
    var result = new float[BinCount];
    var bytes = new byte[64 * 1024];
    double durationUs = Math.Max(1d, durationMs * 1000d);

    using var stream = new FileStream(recording.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    stream.Position = wave.DataOffset;
    long frame = 0;
    long remaining = wave.DataLength;
    while (remaining > 0)
    {
      int read = stream.Read(bytes, 0, (int)Math.Min(bytes.Length, remaining));
      if (read <= 0)
        throw new EndOfStreamException("The calibration microphone WAV is truncated.");
      remaining -= read;
      int frameBytes = wave.Channels * 2;
      for (int offset = 0; offset + frameBytes <= read; offset += frameBytes, frame++)
      {
        int peak = 0;
        for (int channel = 0; channel < wave.Channels; channel++)
        {
          int sampleOffset = offset + channel * 2;
          short sample = (short)(bytes[sampleOffset] | (bytes[sampleOffset + 1] << 8));
          peak = Math.Max(peak, Math.Abs((int)sample));
        }
        double timeUs = recording.CaptureStartOffsetUs + frame * 1_000_000d / wave.SampleRate;
        int bin = (int)(timeUs / durationUs * BinCount);
        if (bin >= 0 && bin < BinCount)
          result[bin] = Math.Max(result[bin], peak / 32768f);
      }
    }
    return Normalize(result);
  }

  public static float[] FromTimedPeaks(float[] peaks, long[] endFrames, int count, int sampleRate, double durationMs)
  {
    var result = new float[BinCount];
    if (peaks == null || endFrames == null || count <= 0 || sampleRate <= 0 || durationMs <= 0d)
      return result;
    count = Math.Min(count, Math.Min(peaks.Length, endFrames.Length));
    double durationFrames = durationMs * sampleRate / 1000d;
    for (int i = 0; i < count; i++)
    {
      long startFrame = i == 0 ? 0L : endFrames[i - 1];
      long endFrame = endFrames[i];
      int firstBin = Math.Max(0, (int)(startFrame / durationFrames * BinCount));
      int lastBin = Math.Min(BinCount - 1, (int)(endFrame / durationFrames * BinCount));
      for (int bin = firstBin; bin <= lastBin; bin++)
        result[bin] = Math.Max(result[bin], peaks[i]);
    }
    return Normalize(result);
  }

  private static float[] Normalize(float[] samples)
  {
    float maximum = 0f;
    for (int i = 0; i < samples.Length; i++)
      maximum = Math.Max(maximum, samples[i]);
    if (maximum <= 0.000001f)
      return samples;
    for (int i = 0; i < samples.Length; i++)
      samples[i] = Math.Min(1f, samples[i] / maximum);
    return samples;
  }
}
