using System;

namespace TUFReplay.Microphone.Capture;

internal static class MicrophoneCaptureChunking
{
  public static int AvailableFrames(int cursor, int position, int clipFrames)
  {
    if (clipFrames <= 0)
      throw new ArgumentOutOfRangeException(nameof(clipFrames));
    if (cursor < 0 || cursor >= clipFrames)
      throw new ArgumentOutOfRangeException(nameof(cursor));
    if (position < 0 || position >= clipFrames)
      throw new ArgumentOutOfRangeException(nameof(position));

    return position >= cursor ? position - cursor : clipFrames - cursor + position;
  }

  public static int NextChunkFrames(int availableFrames, int chunkFrames, bool includePartialChunk)
  {
    if (availableFrames < 0)
      throw new ArgumentOutOfRangeException(nameof(availableFrames));
    if (chunkFrames <= 0)
      throw new ArgumentOutOfRangeException(nameof(chunkFrames));
    if (availableFrames >= chunkFrames)
      return chunkFrames;
    return includePartialChunk ? availableFrames : 0;
  }

  // A reading of capturedFrames at readTicks means the first sample was captured
  // no later than readTicks minus their duration. The tightest bound is the one
  // read right after the device delivered a block.
  public static long RefineStartTimestamp(
    long currentTicks,
    long readTicks,
    long capturedFrames,
    int sampleRate,
    long ticksPerSecond
  )
  {
    if (readTicks <= 0 || capturedFrames < 0 || sampleRate <= 0 || ticksPerSecond <= 0)
      return currentTicks;
    long candidate = readTicks - (long)(capturedFrames * (double)ticksPerSecond / sampleRate);
    return currentTicks <= 0 || candidate < currentTicks ? candidate : currentTicks;
  }

  public static int AdvanceCursor(int cursor, int frames, int clipFrames)
  {
    if (clipFrames <= 0)
      throw new ArgumentOutOfRangeException(nameof(clipFrames));
    if (cursor < 0 || cursor >= clipFrames)
      throw new ArgumentOutOfRangeException(nameof(cursor));
    if (frames < 0 || frames > clipFrames)
      throw new ArgumentOutOfRangeException(nameof(frames));

    return (int)(((long)cursor + frames) % clipFrames);
  }
}
