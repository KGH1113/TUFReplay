using System;
using System.IO;
using System.Text;

namespace TUFReplay.Application.Calibration;

public sealed class CalibrationReferenceWaveform
{
  private const string Magic = "TUFWRF1\0";
  private const int MaximumPeakCount = 10_000_000;

  public int SampleRate;
  public int FramesPerPeak;
  public ushort[] Peaks;

  public static CalibrationReferenceWaveform Read(string path)
  {
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new BinaryReader(stream, Encoding.UTF8, true);
    string magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
    if (!string.Equals(magic, Magic, StringComparison.Ordinal))
      throw new InvalidDataException("The calibration reference waveform header is invalid.");
    int sampleRate = checked((int)reader.ReadUInt32());
    int framesPerPeak = checked((int)reader.ReadUInt32());
    int peakCount = checked((int)reader.ReadUInt32());
    if (sampleRate <= 0 || framesPerPeak <= 0 || peakCount <= 0 || peakCount > MaximumPeakCount)
      throw new InvalidDataException("The calibration reference waveform metadata is invalid.");
    if (stream.Length - stream.Position != peakCount * 2L)
      throw new InvalidDataException("The calibration reference waveform length is invalid.");
    var peaks = new ushort[peakCount];
    for (int i = 0; i < peaks.Length; i++)
      peaks[i] = reader.ReadUInt16();
    return new CalibrationReferenceWaveform
    {
      SampleRate = sampleRate,
      FramesPerPeak = framesPerPeak,
      Peaks = peaks,
    };
  }
}
