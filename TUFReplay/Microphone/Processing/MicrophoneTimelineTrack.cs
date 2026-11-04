using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Timing;

namespace TUFReplay.Microphone.Processing;

// Renderers place a recording with its single capture offset. Rewriting it once
// keeps that offset correct everywhere: audio recorded while gameplay stood still
// between two sections is cut, and an EnhancedCountdown wait is silenced.
public static class MicrophoneTimelineTrack
{
  private const int CopyBufferBytes = 64 * 1024;
  private const int FrameBytes = 2;

  public static bool NeedsRewrite(StoredMicrophoneRecording recording) =>
    recording?.Channels == 1
    && recording.SampleRate == 48000
    && MicrophoneTimelineMap.HasPoints(recording.Timeline)
    && (
      MicrophoneTimelineMap.AudibleFromFrame(recording.Timeline, recording.SampleRate) > 0L
      || CutRanges(recording.Timeline, recording.SampleRate, recording.FrameCount).Count > 0
    );

  // Returns the frame count of the rewritten recording at destinationPath.
  public static long Rewrite(
    StoredMicrophoneRecording recording,
    string destinationPath,
    CancellationToken cancellationToken
  )
  {
    Pcm16WaveInfo wave = Pcm16WaveFile.ReadAndValidate(recording);
    if (wave.Channels != 1 || wave.SampleRate != 48000)
      throw new InvalidDataException("Only 48 kHz mono microphone recordings can be aligned for rendering.");
    List<FrameRange> cuts = CutRanges(recording.Timeline, wave.SampleRate, wave.FrameCount);
    long silentUntil = Math.Min(
      wave.FrameCount,
      MicrophoneTimelineMap.AudibleFromFrame(recording.Timeline, wave.SampleRate)
    );

    long written = 0L;
    var buffer = new byte[CopyBufferBytes];
    using (var source = new FileStream(recording.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
    {
      Pcm16WavWriter.WriteHeader(destination, 0L);
      long frame = 0L;
      int cut = 0;
      while (frame < wave.FrameCount)
      {
        cancellationToken.ThrowIfCancellationRequested();
        while (cut < cuts.Count && cuts[cut].End <= frame)
          cut++;
        if (cut < cuts.Count && cuts[cut].Start <= frame)
        {
          frame = cuts[cut].End;
          continue;
        }

        long next = cut < cuts.Count ? Math.Min(cuts[cut].Start, wave.FrameCount) : wave.FrameCount;
        if (frame < silentUntil)
          next = Math.Min(next, silentUntil);
        int frames = (int)Math.Min(next - frame, buffer.Length / FrameBytes);
        int bytes = frames * FrameBytes;
        source.Position = wave.DataOffset + frame * FrameBytes;
        ReadExactly(source, buffer, bytes);
        if (frame < silentUntil)
          Array.Clear(buffer, 0, bytes);
        destination.Write(buffer, 0, bytes);
        frame += frames;
        written += frames;
      }
      Pcm16WavWriter.WriteHeader(destination, written);
    }
    return written;
  }

  internal static List<FrameRange> CutRanges(MicrophoneTimeline timeline, int sampleRate, long frameCount)
  {
    var cuts = new List<FrameRange>();
    MicrophoneTimelinePoint[] points = timeline.Points;
    for (int index = 1; index < points.Length; index++)
    {
      MicrophoneTimelinePoint point = points[index];
      if (!point.StartsSection)
        continue;
      // Where the previous section would have continued to without the pause.
      MicrophoneTimelinePoint previous = points[index - 1];
      double continuedUs =
        previous.AudioTimeUs
        + (point.TimelineTimeUs - previous.TimelineTimeUs) / MicrophoneTimelineMap.ValidRate(previous.GameplayRate);
      long start = Math.Max(0L, (long)Math.Round(continuedUs * sampleRate / 1_000_000d));
      long end = Math.Min(frameCount, (long)Math.Round(point.AudioTimeUs * (double)sampleRate / 1_000_000d));
      if (end > start)
        cuts.Add(new FrameRange(start, end));
    }
    return cuts;
  }

  private static void ReadExactly(Stream stream, byte[] buffer, int count)
  {
    int offset = 0;
    while (offset < count)
    {
      int read = stream.Read(buffer, offset, count - offset);
      if (read <= 0)
        throw new EndOfStreamException("The microphone WAV is truncated.");
      offset += read;
    }
  }

  internal readonly struct FrameRange
  {
    public readonly long Start;
    public readonly long End;

    public FrameRange(long start, long end)
    {
      Start = start;
      End = end;
    }
  }
}
