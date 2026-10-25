using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;
using static TestFixture;

// End-to-end camera recording through the native relay: synthetic capture output
// in real pipes, the relay's timestamped stream, and a real FFmpeg encoder.
internal static class FfmpegCameraRecordingSuite
{
  public static void RunAll(string root)
  {
    UnusedPreparedRecordingLeavesNoVideo(root);
    string executable = FindFfmpeg();
    if (executable == null)
    {
      Console.WriteLine("SKIP camera recorder FFmpeg integration: FFmpeg is not installed.");
      return;
    }
    if (!SyntheticCameraCapture.IsAvailable)
    {
      Console.WriteLine("SKIP camera recorder FFmpeg integration: the camera relay library is not built.");
      return;
    }
    SavedVideoKeepsCaptureGapsAndExcludesPreRunFrames(executable, root);
    AttachedRunLimitControlsTheActualSavedFile(executable, root);
    Console.WriteLine("TUFReplay camera recorder integration tests passed.");
  }

  private static long Microseconds(long microseconds) => microseconds * Stopwatch.Frequency / 1_000_000;

  private static void SavedVideoKeepsCaptureGapsAndExcludesPreRunFrames(string executable, string root)
  {
    var size = new CameraFrameSize(12, 8);
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("quality").FitToSource(size.Width, size.Height);
    string partial = Path.Combine(root, "camera-gaps.mp4.partial");
    string destination = Path.Combine(root, "camera-gaps.mp4");
    var messages = new List<string>();
    var recording = new FfmpegCameraRecording(
      executable,
      null,
      partial,
      "synthetic",
      profile,
      size,
      32 * 1024 * 1024,
      log: messages.Add
    );
    var pixels = new byte[size.Width * size.Height * 3 / 2];
    Array.Fill(pixels, (byte)128);
    long inputStart = Microseconds(4_000_000);
    long firstCapture = inputStart + Microseconds(100_000);
    bool finished = false;
    using var capture = new SyntheticCameraCapture(size.Width, size.Height);
    try
    {
      // A recent pre-run camera frame must be excluded, not reused.
      capture.Emit(pixels, inputStart - Microseconds(1));
      Assert(capture.WaitReady(), "The synthetic camera never became ready.");
      capture.Now = inputStart;
      recording.Attach("gap-run", destination, 32 * 1024 * 1024, inputStart);
      recording.Connect(capture);
      foreach (long offset in new[] { 0L, 1_960_000, 1_980_000, 2_000_000 })
      {
        long before = recording.RelayStats.WrittenFrames;
        capture.Emit(pixels, firstCapture + Microseconds(offset));
        Assert(
          SpinWait.SpinUntil(() => recording.RelayStats.WrittenFrames > before, TimeSpan.FromSeconds(5)),
          "The relay did not stream an eligible camera frame to the encoder."
        );
      }
      recording.Seal();
      capture.Emit(pixels, firstCapture + Microseconds(2_100_000)); // After the run boundary.
      WebcamRecording saved = recording.FinishAsync().GetAwaiter().GetResult();
      finished = true;
      Assert(saved != null && File.Exists(destination), "The camera recording was not saved.");
      Assert(!File.Exists(partial), "A finalized camera recording left its staging file behind.");
      Assert(saved.RunId == "gap-run", "Prepared camera recording was saved against the wrong run.");
      Assert(saved.CaptureStartTimestampTicks == firstCapture, "Camera synchronization used an excluded first frame.");
      Assert(saved.DurationUs == 2_016_666, "Capture gaps shortened the saved camera duration.");
      Assert(!saved.SizeLimited, "A short recording was treated as size limited.");
      Assert(messages.Any(message => message.Contains("dropped=0")), "Camera diagnostics omitted frame drops.");
      string checksums = ReadPackets(executable, destination);
      Assert(checksums.Contains("#tb 0: 1/1000000"), "Saved MP4 lost the encoder capture time base.");
      string[] mediaTypes = checksums.Split('\n').Where(line => line.StartsWith("#media_type ")).ToArray();
      Assert(
        mediaTypes.Length == 1 && mediaTypes[0].TrimEnd().EndsWith(": video"),
        "Camera capture unexpectedly saved an audio stream."
      );
      long[] timestamps = PacketTimestamps(checksums);
      Assert(
        timestamps.SequenceEqual(new[] { 0L, 1_960_000, 1_980_000, 2_000_000 }),
        "The saved MP4 collapsed a capture gap: " + string.Join(", ", timestamps)
      );
    }
    finally
    {
      if (!finished)
        recording.CancelAsync().GetAwaiter().GetResult();
    }
  }

  private static void UnusedPreparedRecordingLeavesNoVideo(string root)
  {
    var size = new CameraFrameSize(12, 8);
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("compact").FitToSource(size.Width, size.Height);
    string emptyPath = Path.Combine(root, "camera-unused.mp4.partial");
    var empty = new FfmpegCameraRecording("ffmpeg", null, emptyPath, "synthetic", profile, size, 8 * 1024 * 1024);
    Assert(empty.FinishAsync().GetAwaiter().GetResult() == null, "An unattached prepared encoder created a recording.");
    Assert(!File.Exists(emptyPath), "An unused prepared encoder retained an empty camera file.");
    string cancelledPath = Path.Combine(root, "camera-cancelled.mp4.partial");
    var cancelled = new FfmpegCameraRecording(
      "ffmpeg",
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
    var recording = new FfmpegCameraRecording(executable, null, partial, "synthetic", profile, size, 32 * 1024 * 1024);
    bool finished = false;
    using var capture = new SyntheticCameraCapture(size.Width, size.Height);
    try
    {
      var random = new Random(7931);
      var pixels = new byte[size.Width * size.Height * 3 / 2];
      long start = Microseconds(10_000_000);
      capture.Emit(pixels, start - Microseconds(1_000_000));
      Assert(capture.WaitReady(), "The synthetic camera never became ready.");
      capture.Now = start;
      recording.Attach("limited-run", destination, runLimit, start);
      recording.Connect(capture);
      // Noise exceeds the 2 MiB run limit quickly at the quality bitrate. Wait for
      // each write so queue replacement cannot make the fixture tiny.
      for (int index = 0; index < 600; index++)
      {
        CameraRelayRecordingStats before = recording.RelayStats;
        if (before.WriterFinished != 0)
          break;
        random.NextBytes(pixels);
        capture.Emit(pixels, start + Microseconds(index * 16_667L));
        Assert(
          SpinWait.SpinUntil(
            () => recording.RelayStats.WrittenFrames > before.WrittenFrames || recording.RelayStats.WriterFinished != 0,
            TimeSpan.FromSeconds(5)
          ),
          "Size-limit recording stopped consuming frames without finishing."
        );
      }
      WebcamRecording saved = recording.FinishAsync().GetAwaiter().GetResult();
      finished = true;
      Assert(saved != null && File.Exists(destination), "Reaching the run size limit discarded the saved recording.");
      long bytes = new FileInfo(destination).Length;
      Assert(bytes > 512 * 1024, "Size-limit fixture did not produce enough encoded video to exercise the cap.");
      Assert(bytes <= runLimit, "Prepared encoder ignored the attached run's byte limit: " + bytes);
      Assert(saved.SizeLimited, "The saved recording did not report its actual FFmpeg size cutoff.");
      Assert(!File.Exists(partial), "A size-limited recording left its staging file behind.");
      long[] packets = PacketTimestamps(ReadPackets(executable, destination));
      Assert(packets.Length > 1, "The size-limited recording could not be decoded as a multi-frame video.");
      long expectedDuration = packets[packets.Length - 1] + 1_000_000 / profile.FrameRate;
      Assert(
        Math.Abs(saved.DurationUs - expectedDuration) <= 2,
        "Size-limited metadata included frames FFmpeg never saved: expected "
          + expectedDuration
          + ", got "
          + saved.DurationUs
      );
    }
    finally
    {
      if (!finished)
        recording.CancelAsync().GetAwaiter().GetResult();
    }
  }

  private static long[] PacketTimestamps(string checksums) =>
    checksums
      .Split('\n')
      .Where(line => Regex.IsMatch(line, @"^0,\s*\d+,"))
      .Select(line => long.Parse(line.Split(',')[2].Trim(), CultureInfo.InvariantCulture))
      .ToArray();

  internal static string FindFfmpeg()
  {
    string configured = Environment.GetEnvironmentVariable("TUFREPLAY_TEST_FFMPEG");
    if (!string.IsNullOrEmpty(configured))
      return File.Exists(configured) ? configured : null;
    return (Environment.GetEnvironmentVariable("PATH") ?? "")
      .Split(Path.PathSeparator)
      .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"))
      .FirstOrDefault(File.Exists);
  }

  internal static string ReadPackets(string executable, string path)
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
