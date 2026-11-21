using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TUFReplay.Replay.Rendering;
using TUFReplay.Replay.Rendering.Webcam;
using TUFReplay.Webcam.Capture;
using static TestFixture;

// A hosted render draws the recorded webcam itself. Its timing and placement must match what
// OrbitRender's composition drew, and FFmpeg must hand it the right frame for each time.
internal static class RenderWebcamSuite
{
  internal static void RunAll(string root)
  {
    FollowsTheReplayClockWithoutSegments();
    FollowsTheRecordedTimelineSegments();
    ShowsNoCameraOutsideTheRecording();
    PlacesTheCameraOnTheSamePixelsAsOrbitRender();
    DecodesTheFrameForEachCameraTime(root);
    Console.WriteLine("TUFReplay render webcam tests passed.");
  }

  private static JObject Webcam(JArray timeline = null, double durationUs = 60_000_000) =>
    new JObject
    {
      ["durationUs"] = durationUs,
      ["captureStartOffsetUs"] = 500_000,
      ["gameplayRate"] = 1.5,
      ["timeline"] = timeline ?? new JArray(),
      ["offsetMs"] = 20,
      ["mirror"] = true,
      ["crop"] = new JObject
      {
        ["left"] = 0,
        ["top"] = 0,
        ["right"] = 1,
        ["bottom"] = 1,
      },
      ["layout"] = new JObject
      {
        ["left"] = .72,
        ["top"] = .72,
        ["width"] = .25,
        ["height"] = .25,
      },
    };

  // Gameplay starts 2 s into the video at 1.5x; the clear is 10 s of replay time in.
  private static readonly RenderTimeline Timeline = new RenderTimeline(2_000_000, 1.5, 10_000_000);

  private static void FollowsTheReplayClockWithoutSegments()
  {
    RenderWebcamPlan plan = RenderWebcamPlan.Parse(Webcam(), "/camera.mp4");
    // 3 s of video into gameplay is 4.5 s of replay, 3 s of camera, less the capture start.
    Near(plan.SourceTimeUs(Timeline, 5_000_000), 3_000_000 - 500_000 + 20_000, "the replay clock");
    // After the clear the camera runs at real time, like the replay.
    double afterClear = Timeline.ReplayToOutput(12_000_000);
    Near(
      plan.SourceTimeUs(Timeline, afterClear),
      10_000_000 / 1.5 + 2_000_000 - 500_000 + 20_000,
      "real time after the clear"
    );
    Console.WriteLine("PASS the camera follows the replay clock, and real time after the clear");
  }

  private static void FollowsTheRecordedTimelineSegments()
  {
    var segments = new JArray
    {
      new JObject
      {
        ["timelineTimeUs"] = 0,
        ["videoTimeUs"] = 1_000_000,
        ["gameplayRate"] = 1.5,
      },
      new JObject
      {
        ["timelineTimeUs"] = 4_000_000,
        ["videoTimeUs"] = 5_000_000,
        ["gameplayRate"] = 1.0,
      },
    };
    RenderWebcamPlan plan = RenderWebcamPlan.Parse(Webcam(segments), "/camera.mp4");
    // 4.5 s of replay is in the second segment, which runs at its own rate.
    Near(plan.SourceTimeUs(Timeline, 5_000_000), 5_000_000 + 500_000 + 20_000, "the second segment");
    Near(
      plan.SourceTimeUs(Timeline, Timeline.ReplayToOutput(3_000_000)),
      1_000_000 + 3_000_000 / 1.5 + 20_000,
      "the first segment"
    );
    Console.WriteLine("PASS the camera follows the recorded pause and speed segments");
  }

  private static void ShowsNoCameraOutsideTheRecording()
  {
    RenderWebcamPlan plan = RenderWebcamPlan.Parse(Webcam(durationUs: 3_000_000), "/camera.mp4");
    Assert(!plan.SourceTimeUs(Timeline, 0).HasValue, "The camera showed before its recording started.");
    Assert(
      !plan.SourceTimeUs(Timeline, 6_000_000).HasValue,
      "The camera showed after its recording ended, where OrbitRender showed none."
    );
    AssertThrows<InvalidOperationException>(
      () => RenderWebcamPlan.Parse(new JObject { ["durationUs"] = 1 }, "/camera.mp4"),
      "A camera without its timing was accepted."
    );
    Console.WriteLine("PASS no camera shows before or after its recording");
  }

  private static void PlacesTheCameraOnTheSamePixelsAsOrbitRender()
  {
    RenderWebcamLayout inside = RenderWebcamPlan.Parse(Webcam(), "/camera.mp4").Layout(1920, 1080);
    Assert(
      inside.X == 1382 && inside.Y == 778 && inside.Width == 480 && inside.Height == 270,
      "The camera moved from OrbitRender's pixels: "
        + inside.X
        + ","
        + inside.Y
        + " "
        + inside.Width
        + "x"
        + inside.Height
    );
    Assert(
      inside.Filter
        == "crop=iw*1:ih*1:iw*0:ih*0,hflip,crop=max(2\\,iw*1):max(2\\,ih*1):iw*0:ih*0,scale=480:270:flags=lanczos",
      "The camera's crop, mirror or size changed: " + inside.Filter
    );
    JObject partly = Webcam();
    partly["layout"]["left"] = .9;
    RenderWebcamLayout edge = RenderWebcamPlan.Parse(partly, "/camera.mp4").Layout(1920, 1080);
    Assert(
      edge.X == 1728 && edge.Width == 192 && edge.Filter.Contains("crop=max(2\\,iw*0.4):"),
      "A camera partly off the video did not keep only its visible part: " + edge.Filter
    );
    JObject outside = Webcam();
    outside["layout"]["left"] = 1.5;
    Assert(
      RenderWebcamPlan.Parse(outside, "/camera.mp4").Layout(1920, 1080) == null,
      "A camera entirely off the video was drawn."
    );
    Console.WriteLine("PASS the camera lands on OrbitRender's pixels and keeps only its visible part");
  }

  private static void DecodesTheFrameForEachCameraTime(string root)
  {
    string ffmpeg = FfmpegCameraRecordingSuite.FindFfmpeg();
    if (ffmpeg == null)
    {
      Console.WriteLine("SKIP webcam decoding: no FFmpeg (set TUFREPLAY_TEST_FFMPEG)");
      return;
    }
    string directory = Path.Combine(root, "render webcam");
    Directory.CreateDirectory(directory);
    string video = Path.Combine(directory, "camera.mp4");
    // One red second, then one blue, at 10 fps, starting at 0.5 s as recordings can.
    using (
      Process process = FfmpegCameraProcess.Create(
        ffmpeg,
        new[]
        {
          "-v",
          "error",
          "-y",
          "-f",
          "lavfi",
          "-i",
          "color=red:s=64x36:r=10:d=1[a];color=blue:s=64x36:r=10:d=1[b];[a][b]concat=n=2:v=1",
          "-pix_fmt",
          "yuv420p",
          "-c:v",
          "libx264",
          "-g",
          "5",
          "-colorspace",
          "bt709",
          "-color_primaries",
          "bt709",
          "-color_trc",
          "bt709",
          "-color_range",
          "tv",
          "-output_ts_offset",
          "0.5",
          video,
        }
      )
    )
    {
      Assert(process.Start(), "FFmpeg did not start to make the test camera.");
      process.StandardInput.Close();
      string errors = process.StandardError.ReadToEnd();
      process.WaitForExit();
      Assert(process.ExitCode == 0, "The test camera was not made: " + errors);
    }
    using var decoder = new WebcamFrameDecoder(ffmpeg, video, "scale=8:4:flags=lanczos", 8, 4);
    Assert(decoder.Advance(450_000), "The first camera frame was not shown.");
    Assert(decoder.CurrentTimeUs == 400_000, "The frame at or before 0.45 s was not 0.4 s: " + decoder.CurrentTimeUs);
    AssertColor(decoder.Current, red: true, "a red frame");
    Assert(!decoder.Advance(480_000), "The same camera frame was uploaded twice.");
    Assert(decoder.Advance(1_550_000) && decoder.CurrentTimeUs == 1_500_000, "The camera did not move on to 1.5 s.");
    AssertColor(decoder.Current, red: false, "a blue frame");
    // A jump back starts FFmpeg again at that time.
    Assert(decoder.Advance(250_000) && decoder.CurrentTimeUs == 200_000, "The camera did not go back to 0.2 s.");
    AssertColor(decoder.Current, red: true, "a red frame after going back");
    // Past the end, the last frame stays, also when the jump starts FFmpeg again.
    decoder.Advance(2_100_000);
    Assert(decoder.CurrentTimeUs == 1_900_000, "The last camera frame did not stay: " + decoder.CurrentTimeUs);
    decoder.Advance(5_500_000);
    Assert(decoder.CurrentTimeUs == 1_900_000, "A jump past the end lost the last frame: " + decoder.CurrentTimeUs);
    Console.WriteLine("PASS FFmpeg hands each camera time its frame, also after a jump back");
  }

  private static void AssertColor(byte[] pixels, bool red, string what)
  {
    Assert(pixels != null && pixels.Length == 8 * 4 * 4, "The camera frame is not 8x4 RGBA: " + what + ".");
    int r = pixels[0],
      g = pixels[1],
      b = pixels[2],
      a = pixels[3];
    bool ok = red ? r > 230 && g < 25 && b < 25 : b > 230 && r < 25 && g < 25;
    Assert(ok && a == 255, "The camera's colours are off for " + what + ": " + r + "," + g + "," + b + "," + a);
  }

  private static void Near(double? actual, double expected, string what) =>
    Assert(
      actual.HasValue && Math.Abs(actual.Value - expected) < 1,
      "The camera time for " + what + " is " + actual + ", not " + expected + "."
    );
}
