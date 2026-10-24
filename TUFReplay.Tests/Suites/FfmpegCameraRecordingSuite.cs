using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;
using static TestFixture;

internal static class FfmpegCameraRecordingSuite
{
  public static void RunAll(string root)
  {
    string executable = FindFfmpeg();
    if (executable == null)
    {
      Console.WriteLine("SKIP camera recorder FFmpeg integration: FFmpeg is not installed.");
      return;
    }
    BurstOverloadSavesCaptureTimeline(executable, root);
    UnusedPreparedRecordingLeavesNoVideo(executable, root);
    AttachedRunLimitControlsTheActualSavedFile(executable, root);
    Console.WriteLine("TUFReplay camera recorder integration tests passed.");
  }

  private static void BurstOverloadSavesCaptureTimeline(string executable, string root)
  {
    var size = new CameraFrameSize(12, 8);
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("quality").FitToSource(size.Width, size.Height);
    string partial = Path.Combine(root, "camera-overload.mp4.partial");
    string destination = Path.Combine(root, "camera-overload.mp4");
    var messages = new List<string>();
    var capture = new FfmpegCameraRecording(
      executable,
      null,
      partial,
      "synthetic",
      profile,
      size,
      32 * 1024 * 1024,
      log: messages.Add
    );
    bool finished = false;
    var pool = new CameraFramePool(size.Width * size.Height * 3 / 2, 5);
    try
    {
      long inputStart = Stopwatch.Frequency * 4;
      long firstCapture = inputStart + Stopwatch.Frequency / 10;
      capture.Attach("overload-run", destination, 32 * 1024 * 1024, inputStart);
      Submit(capture, pool, inputStart - 1); // A recent pre-run camera frame must be excluded.
      Submit(capture, pool, firstCapture);
      var written = typeof(FfmpegCameraRecording).GetField(
        "_writtenFrames",
        BindingFlags.Instance | BindingFlags.NonPublic
      );
      Assert(
        SpinWait.SpinUntil(() => (long)written.GetValue(capture) == 1, TimeSpan.FromSeconds(5)),
        "The recording did not encode its first eligible camera frame."
      );
      var queue = (CameraFrameQueue)
        typeof(FfmpegCameraRecording)
          .GetField("_frames", BindingFlags.Instance | BindingFlags.NonPublic)
          .GetValue(capture);
      object gate = typeof(CameraFrameQueue)
        .GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)
        .GetValue(queue);
      // Hold the consumer between frames to reproduce a scheduling stall without
      // depending on machine speed or slowing the real FFmpeg process.
      lock (gate)
      {
        for (int index = 1; index <= 100; index++)
          Submit(capture, pool, firstCapture + index * Stopwatch.Frequency / 50);
      }
      WebcamRecording recording = capture.FinishAsync().GetAwaiter().GetResult();
      finished = true;
      Assert(
        recording != null && File.Exists(destination),
        "A transient overload discarded the complete camera recording."
      );
      Assert(!File.Exists(partial), "A finalized camera recording left its staging file behind.");
      Assert(recording.RunId == "overload-run", "Prepared camera recording was saved against the wrong run.");
      Assert(
        recording.CaptureStartTimestampTicks == firstCapture,
        "Camera synchronization used an excluded or dropped first frame."
      );
      Assert(recording.DurationUs == 2_016_666, "Dropped frames shortened the saved camera duration.");
      Assert(!recording.SizeLimited, "Transient encoder overload was treated as the recording size limit.");
      Assert(queue.Dropped == 97, "The integration fixture did not saturate the production recording queue.");
      Assert(
        messages.Any(message => message.Contains("dropped=97")),
        "Camera diagnostics omitted overload frame drops."
      );
      string checksums = ReadPackets(executable, destination);
      Assert(checksums.Contains("#tb 0: 1/1000000"), "Saved MP4 lost the encoder capture time base.");
      string[] mediaTypes = checksums.Split('\n').Where(line => line.StartsWith("#media_type ")).ToArray();
      Assert(
        mediaTypes.Length == 1 && mediaTypes[0].TrimEnd().EndsWith(": video"),
        "Camera capture unexpectedly saved an audio stream."
      );
      long[] timestamps = checksums
        .Split('\n')
        .Where(line => Regex.IsMatch(line, @"^0,\s*\d+,"))
        .Select(line => long.Parse(line.Split(',')[2].Trim(), CultureInfo.InvariantCulture))
        .ToArray();
      Assert(
        timestamps.SequenceEqual(new[] { 0L, 1_960_000, 1_980_000, 2_000_000 }),
        "The actual saved MP4 collapsed a queue-drop gap: " + string.Join(", ", timestamps)
      );
      var leases = new List<CameraFrame>();
      for (int index = 0; index < 5; index++)
      {
        CameraFrame frame = pool.TryRent();
        Assert(frame != null, "Finishing an overloaded recording leaked a frame lease.");
        leases.Add(frame);
      }
      foreach (CameraFrame frame in leases)
        frame.Release();
    }
    finally
    {
      if (!finished)
      {
        try
        {
          capture.CancelAsync().GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException) { }
      }
    }
  }

  private static void UnusedPreparedRecordingLeavesNoVideo(string executable, string root)
  {
    var size = new CameraFrameSize(12, 8);
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("compact").FitToSource(size.Width, size.Height);
    string emptyPath = Path.Combine(root, "camera-unused.mp4.partial");
    var empty = new FfmpegCameraRecording(executable, null, emptyPath, "synthetic", profile, size, 8 * 1024 * 1024);
    Assert(empty.FinishAsync().GetAwaiter().GetResult() == null, "An unattached prepared encoder created a recording.");
    Assert(!File.Exists(emptyPath), "An unused prepared encoder retained an empty camera file.");
    string cancelledPath = Path.Combine(root, "camera-cancelled.mp4.partial");
    var cancelled = new FfmpegCameraRecording(
      executable,
      null,
      cancelledPath,
      "synthetic",
      profile,
      size,
      8 * 1024 * 1024
    );
    cancelled.CancelAsync().GetAwaiter().GetResult();
    Assert(!File.Exists(cancelledPath), "Cancelling a prepared encoder retained its camera file.");
  }

  private static void AttachedRunLimitControlsTheActualSavedFile(string executable, string root)
  {
    var size = new CameraFrameSize(320, 240);
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("quality").FitToSource(size.Width, size.Height);
    string partial = Path.Combine(root, "camera-run-limit.mp4.partial");
    string destination = Path.Combine(root, "camera-run-limit.mp4");
    const long runLimit = 2 * 1024 * 1024;
    var capture = new FfmpegCameraRecording(executable, null, partial, "synthetic", profile, size, 32 * 1024 * 1024);
    bool finished = false;
    try
    {
      capture.Attach("limited-run", destination, runLimit);
      var pool = new CameraFramePool(size.Width * size.Height * 3 / 2, 3);
      var random = new Random(7931);
      var written = typeof(FfmpegCameraRecording).GetField(
        "_writtenFrames",
        BindingFlags.Instance | BindingFlags.NonPublic
      );
      var writer = (Task)
        typeof(FfmpegCameraRecording)
          .GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)
          .GetValue(capture);
      // Noise exceeds the 2 MiB run limit quickly at the quality profile bitrate.
      // Wait for each write so queue replacement cannot make the fixture tiny.
      for (int index = 0; index < 600 && !writer.IsCompleted; index++)
      {
        long before = (long)written.GetValue(capture);
        CameraFrame frame = pool.TryRent();
        Assert(frame != null, "Size-limit recording leaked a frame lease.");
        try
        {
          random.NextBytes(frame.Pixels);
          frame.TimestampTicks = Stopwatch.Frequency * 10 + index * Stopwatch.Frequency / profile.FrameRate;
          capture.Submit(frame);
        }
        finally
        {
          frame.Release();
        }
        Assert(
          SpinWait.SpinUntil(
            () => (long)written.GetValue(capture) > before || writer.IsCompleted,
            TimeSpan.FromSeconds(5)
          ),
          "Size-limit recording stopped consuming frames without finishing."
        );
      }
      WebcamRecording recording = capture.FinishAsync().GetAwaiter().GetResult();
      finished = true;
      Assert(
        recording != null && File.Exists(destination),
        "Reaching the run size limit discarded the saved recording."
      );
      long bytes = new FileInfo(destination).Length;
      Assert(bytes > 512 * 1024, "Size-limit fixture did not produce enough encoded video to exercise the cap.");
      Assert(bytes <= runLimit, "Prepared encoder ignored the attached run's byte limit: " + bytes);
      Assert(recording.SizeLimited, "The saved recording did not report its actual FFmpeg size cutoff.");
      Assert(!File.Exists(partial), "A size-limited recording left its staging file behind.");
      string checksums = ReadPackets(executable, destination);
      string[] packets = checksums.Split('\n').Where(line => Regex.IsMatch(line, @"^0,\s*\d+,")).ToArray();
      Assert(packets.Length > 1, "The size-limited recording could not be decoded as a multi-frame video.");
      long lastTimestamp = long.Parse(packets[packets.Length - 1].Split(',')[2].Trim(), CultureInfo.InvariantCulture);
      long expectedDuration = lastTimestamp + 1_000_000 / profile.FrameRate;
      Assert(
        Math.Abs(recording.DurationUs - expectedDuration) <= 2,
        "Size-limited metadata included frames FFmpeg never saved: expected "
          + expectedDuration
          + ", got "
          + recording.DurationUs
      );
    }
    finally
    {
      if (!finished)
      {
        try
        {
          capture.CancelAsync().GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException) { }
      }
    }
  }

  private static void Submit(FfmpegCameraRecording recording, CameraFramePool pool, long ticks)
  {
    CameraFrame frame = pool.TryRent();
    Assert(frame != null, "Capture exhausted its bounded frame pool during overload.");
    try
    {
      Array.Fill(frame.Pixels, (byte)128);
      frame.TimestampTicks = ticks;
      recording.Submit(frame);
    }
    finally
    {
      frame.Release();
    }
  }

  private static string FindFfmpeg()
  {
    string configured = Environment.GetEnvironmentVariable("TUFREPLAY_TEST_FFMPEG");
    if (!string.IsNullOrEmpty(configured))
      return File.Exists(configured) ? configured : null;
    return (Environment.GetEnvironmentVariable("PATH") ?? "")
      .Split(Path.PathSeparator)
      .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"))
      .FirstOrDefault(File.Exists);
  }

  private static string ReadPackets(string executable, string path)
  {
    using Process process = FfmpegCameraProcess.Create(
      executable,
      new[] { "-v", "error", "-i", path, "-map", "0", "-c", "copy", "-f", "framemd5", "pipe:1" }
    );
    Assert(process.Start(), "Camera recording packet inspection did not start.");
    process.StandardInput.Close();
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> error = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(10000))
    {
      process.Kill();
      throw new Exception("Camera recording packet inspection timed out.");
    }
    Task.WaitAll(output, error);
    Assert(process.ExitCode == 0, "Saved camera MP4 could not be read: " + error.Result);
    return output.Result;
  }
}
