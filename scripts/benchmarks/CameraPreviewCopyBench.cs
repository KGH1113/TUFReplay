using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

internal static class CameraPreviewCopyBench
{
  private const int HeaderSize = 64;
  private const int WarmupFrames = 10;

  private static int Main(string[] args)
  {
    try
    {
      int frames = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 100;
      if (frames < 50 || frames > 5000)
        throw new ArgumentOutOfRangeException("frames", "Measure between 50 and 5000 frames.");
      Run(args[0], 960, 540, frames);
      Run(args[0], 960, 720, frames);
      return 0;
    }
    catch (Exception exception)
    {
      Console.Error.WriteLine(exception);
      return 1;
    }
  }

  private static void Run(string directory, int width, int height, int frames)
  {
    int byteCount = width * height * 4;
    string path = Path.Combine(directory, "preview.frame");
    using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
      file.SetLength(HeaderSize + byteCount);
    var source = new byte[byteCount];
    var oldPixels = new byte[byteCount];
    var bulkPixels = new byte[byteCount];
    for (int index = 0; index < source.Length; index++)
      source[index] = unchecked((byte)(index * 31 + (index >> 12)));

    using (var mapping = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, HeaderSize + byteCount))
    using (var view = mapping.CreateViewAccessor())
    {
      view.WriteArray(HeaderSize, source, 0, source.Length);
      for (int frame = 0; frame < WarmupFrames; frame++)
      {
        ReadArray(view, oldPixels);
        BulkCopy(view, bulkPixels);
      }
      var oldSamples = new double[frames];
      var bulkSamples = new double[frames];
      for (int frame = 0; frame < frames; frame++)
      {
        // Alternate order to keep first/second-path cache and scheduling effects balanced.
        if ((frame & 1) == 0)
        {
          oldSamples[frame] = MeasureReadArray(view, oldPixels);
          bulkSamples[frame] = MeasureBulk(view, bulkPixels);
        }
        else
        {
          bulkSamples[frame] = MeasureBulk(view, bulkPixels);
          oldSamples[frame] = MeasureReadArray(view, oldPixels);
        }
      }
      for (int index = 0; index < source.Length; index++)
        if (source[index] != oldPixels[index] || source[index] != bulkPixels[index])
          throw new InvalidOperationException("A copy path changed frame bytes at " + index + ".");
      Console.WriteLine(
        "{0}x{1} RGBA, {2} bytes; warmup={3}, samples={4}, exactBytes=true",
        width,
        height,
        byteCount,
        WarmupFrames,
        frames
      );
      Print("ReadArray<byte>", oldSamples);
      Print("AcquirePointer + Marshal.Copy", bulkSamples);
    }
  }

  private static double MeasureReadArray(MemoryMappedViewAccessor view, byte[] pixels)
  {
    long started = Stopwatch.GetTimestamp();
    ReadArray(view, pixels);
    return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
  }

  private static double MeasureBulk(MemoryMappedViewAccessor view, byte[] pixels)
  {
    long started = Stopwatch.GetTimestamp();
    BulkCopy(view, pixels);
    return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
  }

  private static void ReadArray(MemoryMappedViewAccessor view, byte[] pixels)
  {
    if (view.ReadArray(HeaderSize, pixels, 0, pixels.Length) != pixels.Length)
      throw new InvalidOperationException("ReadArray returned an incomplete frame.");
  }

  private static unsafe void BulkCopy(MemoryMappedViewAccessor view, byte[] pixels)
  {
    var handle = view.SafeMemoryMappedViewHandle;
    byte* pointer = null;
    handle.AcquirePointer(ref pointer);
    try
    {
      Marshal.Copy(new IntPtr(pointer + view.PointerOffset + HeaderSize), pixels, 0, pixels.Length);
    }
    finally
    {
      handle.ReleasePointer();
    }
  }

  private static void Print(string name, double[] samples)
  {
    double total = 0;
    foreach (double sample in samples)
      total += sample;
    Array.Sort(samples);
    double p95 = samples[(int)Math.Ceiling(samples.Length * 0.95) - 1];
    Console.WriteLine("  {0}: mean={1:F3}ms p95={2:F3}ms", name, total / samples.Length, p95);
  }
}
