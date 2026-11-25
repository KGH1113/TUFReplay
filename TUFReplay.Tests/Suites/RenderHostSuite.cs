using Newtonsoft.Json.Linq;
using TUFReplay.ModApi.Playback;
using TUFReplay.Replay.Export;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Rendering;
using UnityEngine;
using static TestFixture;

// TUFReplay replays renders as OrbitRender's host: it reads its own recording bundle back and
// tells OrbitRender only where the level is and which media to compose.
internal static class RenderHostSuite
{
  internal static void RunAll(string root)
  {
    ReadsTheBundleItWrote(root);
    TellsOrbitRenderAbsoluteMediaPaths(root);
    RefusesMediaOutsideTheBundle(root);
    LeavesTheCameraItDrawsOutOfTheRequest(root);
    FollowsOrbitRenderFinishingInTheBackground();
    RefusesAnOrbitRenderOlderThanTheMinimum();
    FailedRunsRecordTheFailScreen(root);
    KeepsTheRecordedSpeedMultiplier(root);
    GivesOrbitRenderOverlayVideosAsJson();
    Console.WriteLine("TUFReplay render host tests passed.");
  }

  private static void GivesOrbitRenderOverlayVideosAsJson()
  {
    var videos = new[]
    {
      new ReplayOverlayVideo(
        "Mod.Hand",
        "/cache/hand.mov",
        new RectInt(30, 30, 585, 180),
        new RectInt(40, 860, 585, 180)
      ),
      new ReplayOverlayVideo("Mod.Foot", "/cache/foot.mov", new RectInt(0, 0, 400, 100), new RectInt(-10, 0, 200, 50)),
    };
    JArray json = JArray.Parse(RenderReplayHost.OverlayVideosJson(videos));
    Assert(json.Count == 2, "Not every overlay video reached OrbitRender.");
    JObject hand = (JObject)json[0];
    Assert(
      (string)hand["path"] == "/cache/hand.mov"
        && (int)hand["source"]["x"] == 30
        && (int)hand["source"]["y"] == 30
        && (int)hand["source"]["width"] == 585
        && (int)hand["source"]["height"] == 180
        && (int)hand["target"]["x"] == 40
        && (int)hand["target"]["y"] == 860
        && (int)hand["target"]["width"] == 585
        && (int)hand["target"]["height"] == 180,
      "An overlay video's path or rectangles did not reach OrbitRender."
    );
    Assert(
      (string)json[1]["path"] == "/cache/foot.mov" && (int)json[1]["target"]["x"] == -10,
      "Overlay videos lost their order or a target partly outside the video."
    );
    Assert(
      hand.Properties().Select(property => property.Name).SequenceEqual(new[] { "path", "source", "target" }),
      "OrbitRender was sent overlay video fields it does not read."
    );
    Assert(RenderReplayHost.OverlayVideosJson(Array.Empty<ReplayOverlayVideo>()) == "[]", "No videos must be [].");
    Console.WriteLine("PASS OrbitRender gets overlay videos as paths with source and target pixels");
  }

  private static void ReadsTheBundleItWrote(string root)
  {
    RecordingBundle bundle = RecordingBundle.Load(WriteBundle(root, "read", media: null));
    Assert(
      bundle.Inputs.Length == 3 && bundle.Inputs[0].Key == "A" && bundle.Inputs[0].Down,
      "Recorded keys were not read."
    );
    Assert(bundle.Hits.Length == 1 && bundle.Hits[0].Margin == "Perfect", "Recorded hits were not read.");
    Assert(bundle.Manifest.Replay.TerminalTimeUs == 2_000_000, "The end of the recording was not read.");
    Console.WriteLine("PASS the render host reads the recording bundle TUFReplay exports");
  }

  private static void TellsOrbitRenderAbsoluteMediaPaths(string root)
  {
    var media = new JObject
    {
      ["webcam"] = new JObject { ["path"] = "media/camera.mp4", ["offsetMs"] = 12 },
      ["microphone"] = new JObject { ["path"] = "media/microphone.wav", ["volume"] = 1.5 },
    };
    string manifest = WriteBundle(root, "media", media);
    RecordingBundle bundle = RecordingBundle.Load(manifest);
    JObject request = JObject.Parse(
      RenderJobCoordinator.HostedRequestJson(bundle, new OrbitRenderOptions { OutputDirectory = "/videos" })
    );
    string directory = Path.GetDirectoryName(Path.GetFullPath(manifest));
    Assert((string)request["levelPath"] == "/levels/test.adofai", "The level path did not reach OrbitRender.");
    Assert(
      (int)request["startTile"] == 3 && (double)request["gameInputOffsetMs"] == 15,
      "Replay timing did not reach OrbitRender."
    );
    Assert((long)request["wonTimeUs"] == 1_500_000, "The clear time did not reach OrbitRender.");
    Assert(
      (string)request["media"]["webcam"]["path"] == Path.Combine(directory, "media", "camera.mp4")
        && (int)request["media"]["webcam"]["offsetMs"] == 12,
      "The camera did not reach OrbitRender with an absolute path and its settings."
    );
    Assert(
      (string)request["media"]["microphone"]["path"] == Path.Combine(directory, "media", "microphone.wav"),
      "The microphone did not reach OrbitRender with an absolute path."
    );
    Assert((string)request["options"]["outputDirectory"] == "/videos", "The render options did not reach OrbitRender.");
    Assert(
      request["inputsFile"] == null && request["recordingId"] == null,
      "OrbitRender was sent recording data it no longer reads."
    );
    Console.WriteLine("PASS OrbitRender gets the level, timing and absolute media paths, not the recording");
  }

  // A camera the host draws during the render must not reach OrbitRender, or it would compose it
  // again over the finished video and encode the whole picture twice.
  private static void LeavesTheCameraItDrawsOutOfTheRequest(string root)
  {
    var media = new JObject
    {
      ["webcam"] = new JObject { ["path"] = "media/camera.mp4" },
      ["microphone"] = new JObject { ["path"] = "media/microphone.wav" },
    };
    RecordingBundle bundle = RecordingBundle.Load(WriteBundle(root, "drawn camera", media));
    JObject request = JObject.Parse(
      RenderJobCoordinator.HostedRequestJson(bundle, new OrbitRenderOptions(), webcamDrawnByHost: true)
    );
    Assert(request["media"]["webcam"] == null, "OrbitRender was given the camera the host draws.");
    Assert(request["media"]["microphone"] != null, "The microphone no longer reached OrbitRender.");
    var cameraOnly = new JObject { ["webcam"] = new JObject { ["path"] = "media/camera.mp4" } };
    RecordingBundle onlyCamera = RecordingBundle.Load(WriteBundle(root, "drawn camera only", cameraOnly));
    JObject bare = JObject.Parse(
      RenderJobCoordinator.HostedRequestJson(onlyCamera, new OrbitRenderOptions(), webcamDrawnByHost: true)
    );
    Assert(
      bare["media"] == null || bare["media"].Type == JTokenType.Null,
      "A render with only a host-drawn camera still asked OrbitRender to compose media."
    );
    Console.WriteLine("PASS the camera the host draws stays out of OrbitRender's request");
  }

  // OrbitRender finishes in the background and says how far it is, so the web's bar keeps moving
  // through the minutes of waiting for key viewers and composing.
  private static void FollowsOrbitRenderFinishingInTheBackground()
  {
    OrbitRenderResult composing = OrbitRenderClient.Parse(
      "{\"ok\":true,\"job\":{\"id\":\"j\",\"state\":\"finishing\",\"progress\":1,"
        + "\"finishingStage\":\"composing\",\"finishingProgress\":0.5}}"
    );
    Assert(
      composing.Ok && composing.Job.FinishingStage == "composing" && composing.Job.FinishingProgress == 0.5,
      "OrbitRender's finishing stage and progress were not read."
    );
    Assert(
      Math.Abs(RenderJobCoordinator.FinishingShare(composing.Job) - 0.5) < 1e-9,
      "The composition's progress did not move the bar."
    );
    OrbitRenderResult waiting = OrbitRenderClient.Parse(
      "{\"ok\":true,\"job\":{\"id\":\"j\",\"state\":\"finishing\",\"finishingStage\":\"overlays\"}}"
    );
    Assert(
      waiting.Job.FinishingProgress == null && RenderJobCoordinator.FinishingShare(waiting.Job) == 0,
      "Waiting for key viewer videos moved the bar."
    );
    OrbitRenderResult older = OrbitRenderClient.Parse("{\"ok\":true,\"job\":{\"id\":\"j\",\"state\":\"finishing\"}}");
    Assert(
      older.Job.FinishingStage == null && RenderJobCoordinator.FinishingShare(older.Job) == 0,
      "An OrbitRender that does not report its finish broke the bar."
    );
    Console.WriteLine("PASS the web follows OrbitRender's background finish");
  }

  // OrbitRender 2.8 and 2.9 both have RenderApi 3, so the mod's version decides.
  private static void RefusesAnOrbitRenderOlderThanTheMinimum()
  {
    Assert(
      OrbitRenderClient.IsOlderThanMinimum(new Version("2.8.3"))
        && OrbitRenderClient.IsOlderThanMinimum(new Version("2.8.3.0")),
      "OrbitRender 2.8.3 was taken as new enough."
    );
    Assert(
      !OrbitRenderClient.IsOlderThanMinimum(new Version(OrbitRenderClient.MinimumVersion))
        && !OrbitRenderClient.IsOlderThanMinimum(new Version("2.9.1"))
        && !OrbitRenderClient.IsOlderThanMinimum(new Version("2.10.0"))
        && !OrbitRenderClient.IsOlderThanMinimum(new Version("3.0.0")),
      "A new enough OrbitRender was taken as too old."
    );
    Assert(!OrbitRenderClient.IsOlderThanMinimum(null), "An unknown OrbitRender version was taken as too old.");
    Console.WriteLine("PASS an OrbitRender older than the minimum is refused");
  }

  private static void RefusesMediaOutsideTheBundle(string root)
  {
    var media = new JObject { ["webcam"] = new JObject { ["path"] = "../outside.mp4" } };
    RecordingBundle bundle = RecordingBundle.Load(WriteBundle(root, "escape", media));
    AssertThrows<RecordingFormatException>(
      () => RenderJobCoordinator.HostedRequestJson(bundle, new OrbitRenderOptions()),
      "A media path outside the bundle was sent to OrbitRender."
    );
    Console.WriteLine("PASS media paths outside the bundle are refused");
  }

  // A failed run dies at failedTimeUs and records the fail screen up to terminalTimeUs. Older
  // failed recordings end at the death.
  private static void FailedRunsRecordTheFailScreen(string root)
  {
    RecordingBundle failed = RecordingBundle.Load(
      WriteBundle(root, "failed", media: null, replay => Fail(replay, failedTimeUs: 600_000))
    );
    Assert(
      failed.Manifest.Replay.DeathTimeUs == 600_000 && failed.Manifest.Replay.GameplayEndTimeUs == 600_000,
      "A failed run did not die at its recorded fail time."
    );
    Assert(failed.Inputs.Last().TimeUs == 1_500_000, "Keys recorded on the fail screen were not read.");
    JObject request = JObject.Parse(RenderJobCoordinator.HostedRequestJson(failed, new OrbitRenderOptions()));
    Assert((long)request["wonTimeUs"] == 600_000, "OrbitRender did not follow real time after the death.");

    RecordingBundle older = RecordingBundle.Load(
      WriteBundle(root, "failed-older", media: null, replay => Fail(replay, failedTimeUs: null))
    );
    Assert(
      older.Manifest.Replay.DeathTimeUs == 2_000_000 && older.Manifest.Replay.GameplayEndTimeUs == null,
      "An older failed recording did not die at its end."
    );

    AssertThrows<RecordingFormatException>(
      () => RecordingBundle.Load(WriteBundle(root, "cleared-fail", media: null, replay => replay["failedTimeUs"] = 1)),
      "A cleared run with a fail time was accepted."
    );
    AssertThrows<RecordingFormatException>(
      () => RecordingBundle.Load(WriteBundle(root, "late-fail", media: null, replay => Fail(replay, 2_500_000))),
      "A fail time after the end of the recording was accepted."
    );

    var timeline = new RenderTimeline(1_000_000, 1.5, 600_000);
    Assert(
      timeline.ReplayToOutput(600_000) == 1_400_000 && timeline.ReplayToOutput(1_600_000) == 2_400_000,
      "The video did not follow real time after the death."
    );
    Assert(timeline.OutputToReplay(2_400_000) == 1_600_000, "Video time did not map back past the death.");
    Console.WriteLine("PASS failed runs die at the fail time and keep the fail screen");
  }

  // The game shows the speed multiplier, like the editor's Ctrl+Space speed, after the level
  // name. Replays and renders use the recorded one, not the one set when they start.
  private static void KeepsTheRecordedSpeedMultiplier(string root)
  {
    Assert(RecordedSpeedMultiplier.From(100, 0.75f, 0.75f) == 0.75f, "A Ctrl+Space run lost its speed.");
    Assert(
      Math.Abs(RecordedSpeedMultiplier.From(80, 0.8f * 0.75f, 0.75f) - 0.75f) < 1e-6f,
      "A Ctrl+Space run on a pitched level lost its speed."
    );
    Assert(
      RecordedSpeedMultiplier.From(100, 0.9f, 1f) == 0.9f,
      "A practice-mode run, recorded with multiplier 1 by older versions, lost its slower speed."
    );
    Assert(
      RecordedSpeedMultiplier.From(null, 0.75f, 0.75f) == 0.75f,
      "A run without the level's pitch did not use its recorded multiplier."
    );
    Assert(RecordedSpeedMultiplier.From(null, null, null) == 1f, "A run without speeds did not play at 1x.");
    Assert(
      RecordedSpeedMultiplier.From(new ReplayMetadata { levelPitchPercent = 100, effectivePitch = 0.95f }) == 0.95f,
      "Replay metadata did not give the recorded multiplier."
    );

    RecordingBundle practice = RecordingBundle.Load(
      WriteBundle(root, "speed", media: null, replay => replay["speedMultiplier"] = 0.75)
    );
    Assert(practice.Manifest.Replay.SpeedMultiplier == 0.75, "The render did not read the recorded multiplier.");
    RecordingBundle older = RecordingBundle.Load(WriteBundle(root, "speed-older", media: null));
    Assert(older.Manifest.Replay.SpeedMultiplier == null, "An older bundle got a multiplier it never had.");
    AssertThrows<RecordingFormatException>(
      () => RecordingBundle.Load(WriteBundle(root, "speed-zero", media: null, replay => replay["speedMultiplier"] = 0)),
      "A zero speed multiplier was accepted."
    );
    Console.WriteLine("PASS replays and renders keep the recorded speed multiplier");
  }

  private static void Fail(JObject replay, long? failedTimeUs)
  {
    replay["result"] = "failed";
    replay.Remove("wonTimeUs");
    if (failedTimeUs.HasValue)
      replay["failedTimeUs"] = failedTimeUs.Value;
  }

  private static string WriteBundle(string root, string name, JObject media, Action<JObject> replay = null)
  {
    string directory = Path.Combine(root, "render-host-" + name);
    Directory.CreateDirectory(directory);
    File.WriteAllText(
      Path.Combine(directory, "inputs.csv"),
      RecordingCsvReader.InputsHeader + "\n-1000,A,1,0\n500000,A,0,1\n1500000,A,1,2\n"
    );
    File.WriteAllText(
      Path.Combine(directory, "hits.csv"),
      RecordingCsvReader.HitsHeader + "\n400000,1,0.5,0,0,0,0,1.25,3.14,0,0,0,Perfect\n"
    );
    var manifest = new JObject
    {
      ["schemaVersion"] = 1,
      ["recordingId"] = "run-" + name,
      ["level"] = new JObject { ["path"] = "/levels/test.adofai" },
      ["replay"] = new JObject
      {
        ["gameplayStartSongPosition"] = 1.0,
        ["effectivePitch"] = 1.0,
        ["gameInputOffsetMs"] = 15,
        ["noFailMode"] = false,
        ["judgmentSystem"] = "ModernClassic",
        ["startTile"] = 3,
        ["result"] = "cleared",
        ["wonTimeUs"] = 1_500_000,
        ["terminalTimeUs"] = 2_000_000,
      },
      ["inputsFile"] = "inputs.csv",
      ["hitsFile"] = "hits.csv",
    };
    replay?.Invoke((JObject)manifest["replay"]);
    if (media != null)
      manifest["media"] = media;
    string path = Path.Combine(directory, "manifest.json");
    File.WriteAllText(path, manifest.ToString());
    return path;
  }
}
