using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using TUFReplay.Webcam.Capture;
using static TestFixture;

internal static class FfmpegCameraFrameStreamSuite
{
  public static void RunAll(string root)
  {
    FramesKeepTheirCaptureTimesAndLogicalLengths();
    RejectsRegressingTimeWithoutWritingAPartialPacket();
    FrameWritesDoNotAllocate();
    FfmpegPreservesDroppedFrameGaps(root);
    Console.WriteLine("TUFReplay timestamped camera stream tests passed.");
  }

  private static void FramesKeepTheirCaptureTimesAndLogicalLengths()
  {
    using var bytes = new MemoryStream();
    var stream = new FfmpegCameraFrameStream(bytes, 12, 8, 60, 10_000_000);
    var pixels = Enumerable.Repeat((byte)0xCD, 256).ToArray();
    Array.Fill(pixels, (byte)128, 0, 144);
    long first = 8_000_000_000_000_000;
    stream.WriteFrame(pixels, first);
    stream.WriteFrame(pixels, first + 166_670);
    stream.WriteFrame(pixels, first + 216_000_123_450); // Six hours, no short-block overflow.
    var frames = ReadFrames(bytes.ToArray());
    Assert(frames.Count == 3, "Timestamped transport changed the frame count.");
    Assert(frames[0].Timestamp == 0, "The first frame was not anchored to zero.");
    Assert(frames[1].Timestamp == 16_667, "Capture time was rounded to a nominal frame index.");
    Assert(frames[2].Timestamp == 21_600_012_345, "A long camera gap overflowed its timestamp.");
    Assert(frames.All(frame => frame.Pixels.Length == 144), "Pool bucket capacity leaked into the video stream.");
    Assert(frames.All(frame => frame.Pixels.All(value => value == 128)), "Frame payload was corrupted.");
    Assert(stream.FirstTimestampTicks == first, "Capture origin changed after a dropped-frame gap.");
    Assert(stream.DurationUs == 21_600_029_011, "Duration ignored elapsed capture time.");
  }

  private static void RejectsRegressingTimeWithoutWritingAPartialPacket()
  {
    using var bytes = new MemoryStream();
    var stream = new FfmpegCameraFrameStream(bytes, 2, 2, 30, 1000);
    stream.WriteFrame(new byte[6], 1000);
    long length = bytes.Length;
    bool rejected = false;
    try
    {
      stream.WriteFrame(new byte[6], 999);
    }
    catch (ArgumentOutOfRangeException)
    {
      rejected = true;
    }
    Assert(rejected && bytes.Length == length, "Regressing capture time wrote a partial packet.");
  }

  private static void FrameWritesDoNotAllocate()
  {
    var stream = new FfmpegCameraFrameStream(Stream.Null, 1920, 1080, 60, 10_000_000);
    var pixels = new byte[1920 * 1080 * 3 / 2];
    stream.WriteFrame(pixels, 0);
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int index = 1; index <= 120; index++)
      stream.WriteFrame(pixels, index * 166_667L);
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Assert(allocated == 0, "Timestamped packet construction allocated per frame: " + allocated);
  }

  private static void FfmpegPreservesDroppedFrameGaps(string root)
  {
    string executable = Environment.GetEnvironmentVariable("TUFREPLAY_TEST_FFMPEG");
    if (string.IsNullOrEmpty(executable))
      executable = (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator)
        .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"))
        .FirstOrDefault(File.Exists);
    if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
    {
      Console.WriteLine("SKIP timestamped camera FFmpeg roundtrip: FFmpeg is not installed.");
      return;
    }
    string input = Path.Combine(root, "camera-timestamps.mkv");
    string output = Path.Combine(root, "camera-timestamps.mp4");
    using (var file = File.Create(input))
    {
      var stream = new FfmpegCameraFrameStream(file, 12, 8, 60, 1_000_000);
      var pixels = Enumerable.Repeat((byte)128, 256).ToArray();
      foreach (long time in new[] { 0L, 16_667, 200_000, 40_200_000 })
        stream.WriteFrame(pixels, 9_000_000_000 + time);
    }
    RunFfmpeg(
      executable,
      new[]
      {
        "-v",
        "error",
        "-y",
        "-f",
        "matroska",
        "-i",
        input,
        "-c:v",
        "libx264",
        "-preset",
        "ultrafast",
        "-bf",
        "0",
        "-enc_time_base",
        "1:1000000",
        "-fps_mode",
        "vfr",
        "-video_track_timescale",
        "1000000",
        output,
      }
    );
    string checksums = RunFfmpeg(
      executable,
      new[] { "-v", "error", "-i", output, "-map", "0:v:0", "-c:v", "copy", "-f", "framemd5", "pipe:1" }
    );
    Assert(checksums.Contains("#tb 0: 1/1000000"), "Encoded camera stream lost its microsecond time base.");
    long[] timestamps = checksums
      .Split('\n')
      .Where(line => Regex.IsMatch(line, @"^0,\s*\d+,"))
      .Select(line => long.Parse(line.Split(',')[2].Trim(), CultureInfo.InvariantCulture))
      .ToArray();
    Assert(
      timestamps.SequenceEqual(new[] { 0L, 16_667, 200_000, 40_200_000 }),
      "Encoding dropped frames collapsed capture gaps: " + string.Join(", ", timestamps)
    );
  }

  private static string RunFfmpeg(string executable, string[] arguments)
  {
    using Process process = FfmpegCameraProcess.Create(executable, arguments);
    Assert(process.Start(), "Synthetic camera FFmpeg process did not start.");
    process.StandardInput.Close();
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> error = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(15000))
    {
      process.Kill();
      throw new Exception("Synthetic camera FFmpeg roundtrip timed out.");
    }
    Task.WaitAll(output, error);
    Assert(process.ExitCode == 0, "Synthetic camera FFmpeg roundtrip failed: " + error.Result);
    return output.Result;
  }

  private static List<(long Timestamp, byte[] Pixels)> ReadFrames(byte[] bytes)
  {
    var frames = new List<(long Timestamp, byte[] Pixels)>();
    int offset = 0;
    while (offset < bytes.Length)
    {
      ulong id = ReadVint(bytes, ref offset, true);
      long size = (long)ReadVint(bytes, ref offset, false);
      if (id == 0x18538067)
        continue; // Enter the unknown-length Segment.
      int end = checked(offset + (int)size);
      if (id != 0x1F43B675)
      {
        offset = end;
        continue;
      }
      long timestamp = 0;
      while (offset < end)
      {
        ulong child = ReadVint(bytes, ref offset, true);
        int childSize = (int)ReadVint(bytes, ref offset, false);
        if (child == 0xE7)
        {
          for (int index = 0; index < childSize; index++)
            timestamp = (timestamp << 8) | bytes[offset + index];
        }
        else if (child == 0xA3)
        {
          Assert(bytes[offset] == 0x81 && bytes[offset + 3] == 0x80, "Invalid I420 block flags.");
          Assert(bytes[offset + 1] == 0 && bytes[offset + 2] == 0, "Block timestamp exceeded cluster timestamp.");
          frames.Add((timestamp, bytes.Skip(offset + 4).Take(childSize - 4).ToArray()));
        }
        offset += childSize;
      }
      Assert(offset == end, "Cluster payload size was incorrect.");
    }
    return frames;
  }

  private static ulong ReadVint(byte[] bytes, ref int offset, bool keepMarker)
  {
    byte first = bytes[offset++];
    int mask = 0x80;
    int count = 1;
    while ((first & mask) == 0)
    {
      mask >>= 1;
      count++;
    }
    ulong value = keepMarker ? first : (ulong)(first & (mask - 1));
    for (int index = 1; index < count; index++)
      value = (value << 8) | bytes[offset++];
    return value;
  }
}
