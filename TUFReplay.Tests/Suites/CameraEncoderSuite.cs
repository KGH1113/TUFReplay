using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;

internal static class CameraEncoderSuite
{
  public static void RunAll()
  {
    HardwareSelectionRequiresSuccessfulEncoding();
    MissingHardwareUsesSoftware();
    FailedPreparationCanRetry();
    PreparationIsSharedAndInvalidates();
    ProfilesDoNotShareProbeResults();
    ProbeUsesRecordingOptionsAndRealOutput();
  }

  private static void HardwareSelectionRequiresSuccessfulEncoding()
  {
    var attempted = new List<string>();
    var cache = new FfmpegCameraEncoderCache(
      (_, _, encoder) =>
      {
        attempted.Add(encoder.Name);
        return Task.FromResult(new FfmpegCameraEncoderProbe(encoder.Name == "h264_qsv", "synthetic driver result"));
      }
    );
    FfmpegCameraEncoder selected = cache
      .SelectAsync("ffmpeg", WebcamCaptureProfile.ForQuality("quality"))
      .GetAwaiter()
      .GetResult();
    Check(
      selected.Name == "h264_qsv" && selected.IsHardware,
      "Encoder selection accepted an unusable GPU or skipped usable QSV."
    );
    Check(
      attempted.SequenceEqual(new[] { "h264_nvenc", "h264_qsv" }),
      "Encoder selection continued probing after success."
    );
  }

  private static void MissingHardwareUsesSoftware()
  {
    var attempted = new List<string>();
    var cache = new FfmpegCameraEncoderCache(
      (_, _, encoder) =>
      {
        attempted.Add(encoder.Name);
        return Task.FromResult(new FfmpegCameraEncoderProbe(!encoder.IsHardware, "synthetic driver result"));
      }
    );
    FfmpegCameraEncoder selected = cache
      .SelectAsync("ffmpeg", WebcamCaptureProfile.ForQuality("balanced"))
      .GetAwaiter()
      .GetResult();
    Check(selected == FfmpegCameraEncoder.Software, "Missing hardware did not select the software encoder.");
    Check(
      attempted.SequenceEqual(new[] { "h264_nvenc", "h264_qsv", "h264_amf", "libx264" }),
      "A hardware encoder or software probe was skipped."
    );
    string[] options = selected.GetArguments(WebcamCaptureProfile.ForQuality("quality")).ToArray();
    Check(
      Option(options, "-preset") == "ultrafast" && Option(options, "-tune") == "zerolatency",
      "CPU fallback retained expensive buffering or encoding defaults."
    );
  }

  private static void FailedPreparationCanRetry()
  {
    bool working = false;
    int attempts = 0;
    var cache = new FfmpegCameraEncoderCache(
      (_, _, _) =>
      {
        attempts++;
        return Task.FromResult(new FfmpegCameraEncoderProbe(working, "synthetic failure"));
      }
    );
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("quality");
    bool threw = false;
    try
    {
      cache.SelectAsync("ffmpeg", profile).GetAwaiter().GetResult();
    }
    catch (IOException)
    {
      threw = true;
    }
    Check(threw && attempts == 4, "Preparation succeeded without any functioning encoder.");
    working = true;
    Check(
      cache.SelectAsync("ffmpeg", profile).GetAwaiter().GetResult().IsHardware && attempts == 5,
      "A failed preparation poisoned the cache permanently."
    );
  }

  private static void PreparationIsSharedAndInvalidates()
  {
    int attempts = 0;
    var completion = new TaskCompletionSource<FfmpegCameraEncoderProbe>();
    var cache = new FfmpegCameraEncoderCache(
      (_, _, _) =>
      {
        attempts++;
        return completion.Task;
      }
    );
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("quality");
    Task<FfmpegCameraEncoder> first = cache.SelectAsync("ffmpeg", profile);
    Task<FfmpegCameraEncoder> second = cache.SelectAsync("ffmpeg", profile);
    Check(attempts == 1, "Concurrent camera preparation opened duplicate encoder probes.");
    completion.SetResult(new FfmpegCameraEncoderProbe(true, "synthetic success"));
    Check(
      first.GetAwaiter().GetResult() == second.GetAwaiter().GetResult(),
      "Concurrent callers selected different encoders."
    );
    cache.SelectAsync("ffmpeg", profile).GetAwaiter().GetResult();
    Check(attempts == 1, "A prepared profile was probed again on the next recording.");
    cache.Invalidate("ffmpeg", profile);
    cache.SelectAsync("ffmpeg", profile).GetAwaiter().GetResult();
    Check(attempts == 2, "Runtime encoder failure could not invalidate its cached probe.");
  }

  private static void ProfilesDoNotShareProbeResults()
  {
    int attempts = 0;
    var cache = new FfmpegCameraEncoderCache(
      (_, _, _) =>
      {
        attempts++;
        return Task.FromResult(new FfmpegCameraEncoderProbe(true, "synthetic success"));
      }
    );
    cache.SelectAsync("ffmpeg", WebcamCaptureProfile.ForQuality("quality")).GetAwaiter().GetResult();
    cache.SelectAsync("ffmpeg", WebcamCaptureProfile.ForQuality("compact")).GetAwaiter().GetResult();
    cache.SelectAsync("other-ffmpeg", WebcamCaptureProfile.ForQuality("compact")).GetAwaiter().GetResult();
    Check(attempts == 3, "Changed resolution or executable reused an incompatible encoder probe.");
  }

  private static void ProbeUsesRecordingOptionsAndRealOutput()
  {
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("quality").FitToSource(1280, 720);
    var seen = new List<FfmpegCameraEncoder>();
    var cache = new FfmpegCameraEncoderCache(
      (_, _, encoder) =>
      {
        seen.Add(encoder);
        return Task.FromResult(new FfmpegCameraEncoderProbe(!encoder.IsHardware, "synthetic result"));
      }
    );
    cache.SelectAsync("ffmpeg", profile).GetAwaiter().GetResult();
    foreach (FfmpegCameraEncoder encoder in seen)
    {
      string[] probe = FfmpegCameraEncoder.GetProbeArguments(profile, encoder);
      Check(
        Option(probe, "-i") == "color=c=black:s=1280x720:r=60",
        "Probe did not use the actual recording resolution and frame rate."
      );
      Check(
        Option(probe, "-frames:v") == "3" && Option(probe, "-progress") == "pipe:1",
        "Probe only checked codec availability instead of encoded frames."
      );
      string[] options = encoder.GetArguments(profile).ToArray();
      int offset = Array.IndexOf(probe, "-c:v");
      Check(
        probe.Skip(offset).Take(options.Length).SequenceEqual(options),
        "Probe and recording use different codec options."
      );
      Check(Option(options, "-bf") == "0", "Camera encoder enables reorder buffering.");
      Check(
        Option(options, "-pix_fmt") == (encoder.Name == "h264_qsv" || encoder.Name == "h264_amf" ? "nv12" : "yuv420p"),
        "Camera encoder requests an unsupported pixel format or an unnecessary NVENC color conversion."
      );
      Check(
        Option(options, "-r") == null
          && Option(options, "-fps_mode") == "vfr"
          && Option(options, "-enc_time_base") == "1:1000000",
        "Encoder lost capture timestamp precision or uses an incompatible CFR override."
      );
    }
  }

  private static string Option(string[] arguments, string name)
  {
    int index = Array.IndexOf(arguments, name);
    return index < 0 ? null : arguments[index + 1];
  }

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
