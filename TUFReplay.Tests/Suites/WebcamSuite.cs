using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay;
using TUFReplay.Composition;
using TUFReplay.Microphone.Models;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Playback;
using TUFReplay.Shared.Capture;
using TUFReplay.Shared.Timing;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Playback;
using TUFReplay.Webcam.Recording;
using TUFReplay.Webcam.Repositories;
using TUFReplay.Webcam.Timing;

internal static class WebcamSuite
{
  public static void RunAll(string root)
  {
    TimelineSkipsRecordedPausesAndHandlesRateChanges();
    LatestFrameOwnershipRemainsStableAndRejectsStaleFrames();
    AttachingAnEarlierCameraFrameKeepsReplaySynchronized();
    CameraTailSurvivesMicrophoneCompletionUntilEditorOrRetry();
    FailureCameraTailUsesRealTime();
    CameraCompletionKeepsHealthyCaptureArmed(root);
    WonTailAndLatencyHaveTheSameConventionAsMicrophone();
    WallClockPreservesLargeTimestampPrecision();
    FfmpegDiscoverySeparatesAudioFromVideo();
    SettingsRejectInvalidValuesAndKeepTheCameraOffByDefault();
    OverlayAllowsOffscreenPlacementWithoutMargins();
    SharedPreviewFramesAreBoundedAndNeverTorn();
    MaximumPreviewFramesUseExactAllocationFreeCopies();
    DiagnosticSnapshotsPreserveErrorsAndPreviewState();
    PreviewRemainsReadyWhileTheNextFrameIsWritten();
    HiddenPreviewStillWarmsUpAndSkipsUnusedFrames();
    PreviewReadsNeverWaitForTheProducer();
    CameraDownscalingPreservesNativeAspect();
    PreviewConversionPreservesColorAndOrientation();
    PreviewDownscalingAveragesFineDetailWithoutAllocating();
    DispositionCompletesOnceInEitherOrder();
    DispositionWaitsForActivityPersistence();
    StoreProtectsPlaybackAndEnforcesRetention(root);
    StoreDeletionRejectsLateCapture(root);
    StoreRejectsCorruptTimingAndDuplicateSaves(root);
    StoreReleaseNeverWaitsForRetention(root);
    FfmpegUsesLogicalPooledFrameSize(root);
    PreparedFfmpegWriterKeepsIdleTimeOutOfVideo(root);
    LargeCameraBuffersAreReused();
    Console.WriteLine("TUFReplay webcam timing, storage, and disposition tests passed.");
  }

  private static long Ticks(double seconds) => (long)Math.Round(seconds * Stopwatch.Frequency);

  private static void FailureCameraTailUsesRealTime()
  {
    var timeline = new WebcamCaptureTimeline();
    timeline.Observe(new CaptureTimelineAnchor(Ticks(10), 0, 1.5));
    timeline.Observe(new CaptureTimelineAnchor(Ticks(12), 3_000_000, 1));
    var recording = new WebcamRecording { CaptureStartTimestampTicks = Ticks(10) };
    timeline.ApplyTo(recording);
    Near(2, WebcamPlaybackClock.ToVideoSeconds(Snapshot(3_000_000), recording, 0));
    Near(4.5, WebcamPlaybackClock.ToVideoSeconds(Snapshot(5_500_000), recording, 0));
    Check(recording.Timeline.Last().GameplayRate == 1, "Failure camera tail retained the gameplay pitch.");
  }

  private static void CameraTailSurvivesMicrophoneCompletionUntilEditorOrRetry()
  {
    PropertyInfo cameraProperty = typeof(FeatureRegistry).GetProperty(nameof(FeatureRegistry.WebcamRecording));
    WebcamRecordingFeature previous = FeatureRegistry.WebcamRecording;
    try
    {
      foreach (bool retry in new[] { false, true })
      {
        var backend = new CompletedCameraBackend(null);
        var camera = new WebcamRecordingFeature();
        SetCameraField(camera, "_backend", backend);
        SetCameraField(camera, "_armed", true);
        SetCameraField(camera, "_recording", true);
        SetCameraField(camera, "_runId", "camera-tail");
        cameraProperty.SetValue(null, camera);
        var feature = (RecordingFeature)
          System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RecordingFeature));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(RecordingFeature).GetField("_webcamCaptureStarted", flags).SetValue(feature, true);
        typeof(RecordingFeature).GetField("_webcamTimeline", flags).SetValue(feature, new WebcamCaptureTimeline());
        typeof(RecordingFeature).GetField("_runPersistence", flags).SetValue(feature, Task.FromResult(false));
        typeof(RecordingFeature).GetField("_persistFailedWebcam", flags).SetValue(feature, true);
        int completions = 0;
        Action<CapturedMicrophoneRecording> completed = _ => completions++;
        MethodInfo end = typeof(RecordingFeature).GetMethod("EndMicrophoneRun", flags);
        end.Invoke(feature, new object[] { completed, false, false });
        Check(completions == 1 && backend.EndCalls == 0, "Microphone completion stopped the failure camera tail.");
        if (retry)
          typeof(RecordingFeature).GetMethod("ResetRunState", flags).Invoke(feature, null);
        else
          end.Invoke(feature, new object[] { completed, true, true });
        Task work = (Task)typeof(WebcamRecordingFeature).GetField("_work", flags).GetValue(camera);
        Check(work.Wait(TimeSpan.FromSeconds(5)), "Camera tail finalization did not finish.");
        Check(
          backend.EndCalls == 1 && backend.DisarmCalls == 0,
          "Editor or retry failed to detach exactly one camera run."
        );
        end.Invoke(feature, new object[] { completed, false, true });
        Check(backend.EndCalls == 1, "Session cleanup finalized the same camera tail twice.");
        if (retry)
          Check(
            !(bool)typeof(RecordingFeature).GetField("_persistFailedWebcam", flags).GetValue(feature),
            "A failed run's camera disposition leaked into its retry."
          );
      }
    }
    finally
    {
      cameraProperty.SetValue(null, previous);
    }
  }

  private static void PreparedFfmpegWriterKeepsIdleTimeOutOfVideo(string root)
  {
    string executable = Environment.GetEnvironmentVariable("TUFREPLAY_CAMERA_TEST_FFMPEG");
    if (string.IsNullOrWhiteSpace(executable))
      return;
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("compact").FitToSource(640, 360);
    var source = new CameraFrameSize(640, 360);
    string standby = Path.Combine(root, "camera-prepared.mp4.partial");
    string destination = Path.Combine(root, "camera-attached.mp4.partial");
    var recording = new FfmpegCameraRecording(
      executable,
      null,
      standby,
      "synthetic",
      profile,
      source,
      WebcamRecordingStore.MaximumCaptureBytes
    );
    bool finalizing = false;
    try
    {
      // Preparing the encoder must not create footage for the idle interval.
      Thread.Sleep(100);
      Check(!File.Exists(destination), "The idle camera writer used a run destination before attachment.");
      recording.Attach("synthetic-run", destination, WebcamRecordingStore.MaximumCaptureBytes, Ticks(10));
      byte[] pixels = new byte[640 * 360 * 3 / 2];
      Array.Fill(pixels, (byte)128, 640 * 360, pixels.Length - 640 * 360);
      recording.Submit(pixels, Ticks(9.99)); // A cached camera frame from before input capture must be rejected.
      for (int frame = 0; frame < 12; frame++)
      {
        recording.Submit(pixels, Ticks(10 + frame / 30d));
        Thread.Sleep(35);
      }
      finalizing = true;
      WebcamRecording result = recording.FinishAsync().GetAwaiter().GetResult();
      Check(result.RunId == "synthetic-run" && result.FilePath == destination, "Camera attachment lost its run.");
      Check(result.CaptureStartTimestampTicks == Ticks(10), "The prepared encoder changed the first frame clock.");
      // FFmpeg progress reports the final mux timestamp, which can precede
      // the MP4 duration when H.264 reorders frames. Verify the frame count
      // by decoding below rather than treating progress as a frame count.
      Check(
        result.DurationUs > 0 && result.DurationUs <= 400_000,
        "The prepared encoder reported an invalid duration: " + result.DurationUs
      );
      Check(File.Exists(destination) && !File.Exists(standby), "Camera footage kept the standby destination.");
      using var decoder = FfmpegCameraProcess.Create(
        executable,
        new[] { "-v", "error", "-nostats", "-i", destination, "-an", "-progress", "pipe:1", "-f", "null", "-" }
      );
      Check(decoder.Start(), "Synthetic camera decoding did not start.");
      decoder.StandardInput.Close();
      Task<string> progress = decoder.StandardOutput.ReadToEndAsync();
      Task<string> errors = decoder.StandardError.ReadToEndAsync();
      if (!decoder.WaitForExit(10000))
      {
        decoder.Kill();
        throw new InvalidOperationException("Synthetic camera decoding timed out.");
      }
      Check(decoder.ExitCode == 0, "The prepared camera MP4 could not be decoded: " + errors.GetAwaiter().GetResult());
      Check(
        progress.GetAwaiter().GetResult().Split('\n').Where(line => line.StartsWith("frame=")).LastOrDefault()?.Trim()
          == "frame=12",
        "The prepared MP4 saved idle time or did not decode all 12 frames."
      );
    }
    finally
    {
      if (!finalizing)
        recording.CancelAsync().GetAwaiter().GetResult();
    }
    string unusedPath = Path.Combine(root, "camera-unused.mp4.partial");
    var unused = new FfmpegCameraRecording(
      executable,
      null,
      unusedPath,
      "synthetic",
      profile,
      source,
      WebcamRecordingStore.MaximumCaptureBytes
    );
    unused.CancelAsync().GetAwaiter().GetResult();
    Check(!File.Exists(unusedPath), "Cancelling an idle camera encoder leaked its temporary file.");
    Console.WriteLine("TUFReplay prepared FFmpeg camera encoder integration test passed (no camera access).");
  }

  private static void LatestFrameOwnershipRemainsStableAndRejectsStaleFrames()
  {
    var latest = new LatestCameraFrame();
    var first = new byte[] { 1, 2, 3, 4 };
    byte[] reader = latest.Exchange(first, 1000);
    Check(reader != first, "The camera reader received the frame still held for attachment.");
    reader[0] = 9;
    Check(latest.TryGet(1060, 1000, out byte[] pixels, out long ticks), "A current camera frame was discarded.");
    Check(pixels[0] == 1 && ticks == 1000, "Reading the next frame changed the attached frame or its timestamp.");
    Check(!latest.TryGet(999, 1000, out _, out _), "A future camera timestamp was accepted.");
    Check(!latest.TryGet(1251, 1000, out _, out _), "A stale camera frame was appended to a new run.");
    reader = latest.Exchange(reader, 1100);
    Check(reader == first, "Camera frames stopped reusing the bounded pair of buffers.");
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int frame = 0; frame < 100; frame++)
      reader = latest.Exchange(reader, 1100 + frame);
    Check(GC.GetAllocatedBytesForCurrentThread() == before, "Idle camera frame rotation allocated memory.");
    latest.Clear();
    Check(!latest.TryGet(1200, 1000, out _, out _), "Disabling capture retained a frame for a future run.");
  }

  private static void AttachingAnEarlierCameraFrameKeepsReplaySynchronized()
  {
    var timeline = new WebcamCaptureTimeline();
    timeline.Observe(new CaptureTimelineAnchor(Ticks(10), 0, 1));
    var recording = new WebcamRecording { CaptureStartTimestampTicks = Ticks(9.94) };
    timeline.ApplyTo(recording);
    Check(recording.CaptureStartOffsetUs == -60_000, "The seeded camera frame lost its real capture timestamp.");
    Near(0.06, WebcamPlaybackClock.ToVideoSeconds(Snapshot(0), recording, 0));
    Near(0.56, WebcamPlaybackClock.ToVideoSeconds(Snapshot(500_000), recording, 0));
  }

  private static void CameraCompletionKeepsHealthyCaptureArmed(string root)
  {
    for (int variant = 0; variant < 4; variant++)
    {
      var timeline = variant == 1 ? null : new WebcamCaptureTimeline();
      if (variant >= 2)
        timeline.Observe(new CaptureTimelineAnchor(Ticks(11), 0, 1));
      var recording = new WebcamRecording
      {
        RunId = "camera-finalize-" + variant,
        FilePath = Path.Combine(root, "camera-finalize-" + variant + ".mp4"),
        CaptureStartTimestampTicks = variant == 2 ? 0 : Ticks(10),
      };
      File.WriteAllText(recording.FilePath, "temporary camera video");
      var backend = new CompletedCameraBackend(recording);
      var camera = new WebcamRecordingFeature();
      SetCameraField(camera, "_backend", backend);
      SetCameraField(camera, "_armed", true);
      SetCameraField(camera, "_recording", true);
      SetCameraField(camera, "_runId", recording.RunId);
      int callbacks = 0;
      WebcamRecording result = recording;
      camera.EndRun(
        timeline,
        value =>
        {
          result = value;
          Interlocked.Increment(ref callbacks);
        }
      );
      Task completion = (Task)
        typeof(WebcamRecordingFeature)
          .GetField("_work", BindingFlags.NonPublic | BindingFlags.Instance)
          .GetValue(camera);
      Check(completion.Wait(TimeSpan.FromSeconds(5)), "Camera finalization did not complete.");
      bool synchronized = variant == 3;
      Check(callbacks == 1, "Camera completion was invoked more than once.");
      Check(synchronized ? result == recording : result == null, "Camera completion kept the wrong video.");
      Check(camera.IsReady, "An incomplete run made a healthy camera unavailable.");
      Check(backend.EndCalls == 1 && backend.DisarmCalls == 0, "Discarding a run interrupted continuous capture.");
      Check(File.Exists(recording.FilePath) == synchronized, "Camera completion deleted or retained the wrong file.");
      if (synchronized)
      {
        Check(recording.Timeline.Length == 1, "Valid camera synchronization was lost.");
        Check(recording.CaptureStartOffsetUs == -1_000_000, "Valid camera start alignment changed.");
      }
      Check(
        (int)
          typeof(WebcamRecordingFeature)
            .GetField("_finalizing", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(camera) == 0,
        "Discarding a camera run leaked the finalization counter."
      );
    }
  }

  private static void SetCameraField(WebcamRecordingFeature camera, string name, object value) =>
    typeof(WebcamRecordingFeature)
      .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
      .SetValue(camera, value);

  private sealed class CompletedCameraBackend : IWebcamCaptureBackend
  {
    private readonly WebcamRecording _recording;
    public int EndCalls;
    public int DisarmCalls;
    public CameraPreviewBuffer Preview => null;

    public CompletedCameraBackend(WebcamRecording recording) => _recording = recording;

    public Task<WebcamRecording> EndAsync()
    {
      EndCalls++;
      return Task.FromResult(_recording);
    }

    public Task DisarmAsync()
    {
      DisarmCalls++;
      return Task.CompletedTask;
    }

    public Task<List<WebcamDevice>> ListDevicesAsync() => throw new InvalidOperationException("Do not open a camera.");

    public Task ArmAsync(string deviceId, WebcamCaptureProfile profile, string recordingDirectory = null) =>
      throw new InvalidOperationException("Do not open a camera.");

    public Task BeginAsync(string runId, string path, long maxBytes, long startTimestampTicks = 0) =>
      throw new InvalidOperationException("Do not open a camera.");

    public void Dispose() { }
  }

  private static void DiagnosticSnapshotsPreserveErrorsAndPreviewState()
  {
    var failure = new IOException("Camera failed\nTry again", new InvalidOperationException("Device disconnected"));
    JObject record = CaptureDiagnostics.CreateRecord(
      "capture.begin.failed",
      new { operationId = 7, runId = "run" },
      failure
    );
    string line = record.ToString(Formatting.None);
    Check(!line.Contains('\n'), "Camera diagnostics must stay on one JSON log line.");
    JObject parsed = JObject.Parse(line);
    Check((string)parsed["exception"]?["type"] == typeof(IOException).FullName, "Camera exception type was lost.");
    Check(
      ((string)parsed["exception"]?["detail"])?.Contains("Device disconnected") == true,
      "Camera inner exception was lost."
    );
    using var preview = new CameraPreviewBuffer(4, 2);
    JObject before = JObject.FromObject(preview.DiagnosticState());
    Check(
      (long?)before["sequence"] == 0 && (bool?)before["disposed"] == false,
      "Empty preview diagnostics were incorrect."
    );
    preview.Publish(new byte[32]);
    Check(preview.TryCopy(new byte[32]), "Diagnostics changed preview frame consumption.");
    JObject ready = JObject.FromObject(preview.DiagnosticState());
    Check(
      (long?)ready["sequence"] == 2 && (long?)ready["lastReadSequence"] == 2,
      "Preview diagnostic counters were incorrect."
    );
    preview.Dispose();
    JObject disposed = JObject.FromObject(preview.DiagnosticState());
    Check(
      (bool?)disposed["disposed"] == true && disposed["sequence"]?.Type == JTokenType.Null,
      "Disposed preview diagnostics accessed released memory."
    );
    var timeline = new WebcamCaptureTimeline();
    timeline.NoteUnavailableAnchor();
    timeline.Observe(new CaptureTimelineAnchor(Ticks(1), 0, double.NaN));
    timeline.Observe(new CaptureTimelineAnchor(0, 0, 1));
    JObject clockState = JObject.FromObject(timeline.DiagnosticState());
    Check(
      (int?)clockState["unavailableAnchors"] == 1
        && (int?)clockState["invalidRates"] == 1
        && (int?)clockState["invalidTimestamps"] == 1
        && (int?)clockState["segments"] == 0,
      "Camera diagnostics did not distinguish a missing game clock from missing camera frames."
    );
  }

  private static ReplayPlaybackSnapshot Snapshot(long timeUs, double rate = 1, long? won = null, bool paused = false) =>
    new ReplayPlaybackSnapshot(timeUs, rate, 1.5, won, paused);

  private static void TimelineSkipsRecordedPausesAndHandlesRateChanges()
  {
    var timeline = new WebcamCaptureTimeline();
    timeline.Observe(new CaptureTimelineAnchor(Ticks(11), 0, 1.5));
    timeline.Observe(new CaptureTimelineAnchor(Ticks(12), 1_500_000, 1.5));
    // Three real seconds spent paused should disappear from replay time.
    timeline.Observe(new CaptureTimelineAnchor(Ticks(15), 1_500_000, 1.5));
    timeline.Observe(new CaptureTimelineAnchor(Ticks(16), 3_000_000, 0.75));
    var recording = new WebcamRecording { CaptureStartTimestampTicks = Ticks(10) };
    timeline.ApplyTo(recording);
    Check(recording.Timeline.Length == 3, "Normal frames must not allocate timeline segments.");
    Near(1, WebcamPlaybackClock.ToVideoSeconds(Snapshot(0), recording, 0));
    Near(5, WebcamPlaybackClock.ToVideoSeconds(Snapshot(1_500_000), recording, 0));
    Near(8, WebcamPlaybackClock.ToVideoSeconds(Snapshot(4_500_000), recording, 0));
    Near(2, WebcamPlaybackClock.PlaybackRate(Snapshot(4_500_000, 1.5), recording));
    Near(1, WebcamPlaybackClock.ToVideoSeconds(Snapshot(0, paused: true), recording, 0));
  }

  private static void WonTailAndLatencyHaveTheSameConventionAsMicrophone()
  {
    var recording = new WebcamRecording { GameplayRate = 1.5, CaptureStartOffsetUs = -1_000_000 };
    Near(4, WebcamPlaybackClock.ToVideoSeconds(Snapshot(4_000_000, won: 3_000_000), recording, 0));
    Near(4.12, WebcamPlaybackClock.ToVideoSeconds(Snapshot(4_000_000, won: 3_000_000), recording, 120));
    Near(3.88, WebcamPlaybackClock.ToVideoSeconds(Snapshot(4_000_000, won: 3_000_000), recording, -120));
    Near(0.5, WebcamPlaybackClock.PlaybackRate(Snapshot(1_000_000, 0.75), recording));
    Near(1, WebcamPlaybackClock.PlaybackRate(Snapshot(4_000_000, won: 3_000_000), recording));
    var timeline = new WebcamCaptureTimeline();
    timeline.Observe(new CaptureTimelineAnchor(Ticks(11), 0, 1.5));
    timeline.Observe(new CaptureTimelineAnchor(Ticks(13), 3_000_000, 1));
    recording.CaptureStartTimestampTicks = Ticks(10);
    timeline.ApplyTo(recording);
    Near(4, WebcamPlaybackClock.ToVideoSeconds(Snapshot(4_000_000, won: 3_000_000), recording, 0));
  }

  private static void WallClockPreservesLargeTimestampPrecision()
  {
    const long epochUs = 1_790_000_000_000_000;
    var clock = new WebcamWallClock(epochUs, Ticks(10));
    Check(
      Math.Abs(clock.ToTimestamp(epochUs * 10 + 370, 1, 10_000_000) - Ticks(10.000037)) <= 1,
      "Large FFmpeg timestamps lost sub-frame precision."
    );
  }

  private static void SettingsRejectInvalidValuesAndKeepTheCameraOffByDefault()
  {
    var settings = new TUFReplaySetting();
    Check(!settings.WebcamEnabled, "Camera access must require opt-in.");
    foreach (
      string invalid in new[]
      {
        "{\"enabled\":\"true\"}",
        "{\"overlayX\":2.1}",
        "{\"overlayWidth\":0}",
        "{\"offsetMs\":1001}",
        "{\"offsetMs\":1.5}",
        "{\"storageLimitMb\":1}",
        "{\"otherSetting\":true}",
      }
    )
      Check(!WebcamSettingsPatch.TryParse(JObject.Parse(invalid), out _), "Invalid settings were accepted: " + invalid);
    Check(
      WebcamSettingsPatch.TryParse(JObject.Parse("{\"offsetMs\":120,\"overlayX\":0,\"mirror\":true}"), out var patch),
      "Valid settings were rejected."
    );
    patch.ApplyTo(settings);
    Check(
      settings.WebcamOffsetMs == 120
        && settings.WebcamOverlayX == 0
        && settings.WebcamMirror
        && !settings.WebcamEnabled,
      "Partial webcam settings changed unrelated settings."
    );
    settings.WebcamOverlayX = double.NaN;
    settings.WebcamOverlayWidth = double.PositiveInfinity;
    settings.Normalize();
    Near(1, settings.WebcamOverlayX);
    Near(0.22, settings.WebcamOverlayWidth);
  }

  private static void FfmpegDiscoverySeparatesAudioFromVideo()
  {
    var devices = FfmpegCameraDevices.Parse(
      new[]
      {
        "[dshow] \"USB camera\" (video)",
        "[dshow] Alternative name \"@device_video_1\"",
        "[dshow] \"Microphone\" (audio)",
        "[dshow] Alternative name \"@device_audio_1\"",
        "[dshow] \"Camera without alternative\" (video)",
        "[dshow] \"Second microphone\" (audio)",
        "[dshow] Alternative name \"@device_audio_2\"",
      }
    );
    Check(
      devices.Count == 2 && devices[0].Id == "@device_video_1" && devices[1].Id == "Camera without alternative",
      "An audio id replaced a camera id."
    );
    Check(
      CaptureProcessArguments.Quote("C:\\camera path\\") == "\"C:\\camera path\\\\\"",
      "A trailing slash escaped the command-line closing quote."
    );
    Check(
      CaptureProcessArguments.Quote("camera\"name") == "\"camera\\\"name\"",
      "A camera name broke command-line quoting."
    );
  }

  private static void OverlayAllowsOffscreenPlacementWithoutMargins()
  {
    foreach (var size in new[] { (1920d, 1080d), (800d, 600d), (320d, 240d), (600d, 1200d) })
    foreach (double aspect in new[] { 4d / 3, 16d / 9 })
    foreach (double position in new[] { 0d, 0.5, 1d })
    {
      var settings = new TUFReplaySetting
      {
        WebcamOverlayX = position,
        WebcamOverlayY = position,
        WebcamOverlayWidth = 0.5,
      };
      WebcamOverlayBounds bounds = WebcamOverlayLayout.Get(settings, size.Item1, size.Item2, aspect);
      Check(
        bounds.X >= 0
          && bounds.Y >= 0
          && bounds.X + bounds.Width <= size.Item1
          && bounds.Y + bounds.Height <= size.Item2,
        "The webcam overlay left the game window."
      );
      WebcamOverlayLayout.MoveTo(settings, -10000, 10000, size.Item1, size.Item2, aspect);
      Near(-1, settings.WebcamOverlayX);
      Near(2, settings.WebcamOverlayY);
      WebcamOverlayLayout.MoveTo(settings, -bounds.Width / 2, -bounds.Height / 2, size.Item1, size.Item2, aspect);
      WebcamOverlayBounds moved = WebcamOverlayLayout.Get(settings, size.Item1, size.Item2, aspect);
      Near(-bounds.Width / 2, moved.X);
      Near(-bounds.Height / 2, moved.Y);
    }
    var corner = new TUFReplaySetting();
    WebcamOverlayBounds right = WebcamOverlayLayout.Get(corner, 1920, 1080, 16d / 9);
    Near(1920, right.X + right.Width);
    Near(1080, right.Y + right.Height);
    Check(
      WebcamSettingsPatch.TryParse(
        JObject.Parse("{\"quality\":\"quality\",\"liveVisible\":true,\"overlayX\":-0.1,\"overlayY\":1.1}"),
        out var patch
      ),
      "Free camera placement and Quality were rejected."
    );
    patch.ApplyTo(corner);
    corner.Normalize();
    Check(
      corner.WebcamQuality == "quality"
        && corner.WebcamLiveVisible
        && corner.WebcamOverlayX == -0.1
        && corner.WebcamOverlayY == 1.1,
      "Camera settings did not persist."
    );
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality(corner.WebcamQuality);
    Check(
      profile.Width == 1920 && profile.Height == 1080 && profile.FrameRate == 30 && profile.BitRate == 3_000_000,
      "Quality profile changed."
    );
  }

  private static void SharedPreviewFramesAreBoundedAndNeverTorn()
  {
    using var preview = new CameraPreviewBuffer(32, 24);
    var pixels = new byte[preview.ByteCount];
    Check(!preview.HasFrame && !preview.TryCopy(pixels), "An uninitialized camera frame was displayed.");
    using var map = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(preview.Path, FileMode.Open);
    using var writer = map.CreateViewAccessor();
    writer.Write(0, 1L);
    writer.WriteArray(
      CameraPreviewBuffer.HeaderSize,
      Enumerable.Repeat((byte)128, preview.ByteCount).ToArray(),
      0,
      preview.ByteCount
    );
    Check(!preview.TryCopy(pixels), "A frame was read while the helper was writing it.");
    writer.Write(0, 2L);
    Check(preview.TryCopy(pixels) && pixels.All(value => value == 128), "Shared camera pixels were not published.");
    Check(!preview.TryCopy(pixels), "An unchanged camera frame was uploaded twice.");
    preview.Publish(Enumerable.Repeat((byte)64, preview.ByteCount).ToArray());
    Check(preview.TryCopy(pixels) && pixels.All(value => value == 64), "Managed camera pixels were not published.");
    Check(
      new FileInfo(preview.Path).Length == CameraPreviewBuffer.HeaderSize + preview.ByteCount,
      "Camera preview storage grew with time."
    );
  }

  private static void MaximumPreviewFramesUseExactAllocationFreeCopies()
  {
    using var preview = new CameraPreviewBuffer(CameraPreviewBuffer.MaxWidth, CameraPreviewBuffer.MaxHeight);
    var original = new byte[preview.ByteCount];
    var copied = new byte[original.Length];
    for (int index = 0; index < original.Length; index++)
      original[index] = unchecked((byte)(index * 31 + (index >> 12)));
    preview.Publish(original);
    Check(!preview.TryCopy(new byte[original.Length - 4]), "A short destination consumed a maximum-size frame.");
    Check(preview.TryCopy(copied, out CameraFrameSize size), "A maximum-size camera frame could not be copied.");
    Check(
      size.Width == CameraPreviewBuffer.MaxWidth
        && size.Height == CameraPreviewBuffer.MaxHeight
        && original.AsSpan().SequenceEqual(copied),
      "Bulk camera copy included the header or lost frame bytes."
    );
    Check(!preview.TryCopy(copied), "The same maximum-size frame was copied twice.");
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int frame = 0; frame < 8; frame++)
    {
      original[0] = (byte)frame;
      original[original.Length - 1] = (byte)(255 - frame);
      preview.Publish(original);
      Check(
        preview.TryCopy(copied) && copied[0] == frame && copied[copied.Length - 1] == 255 - frame,
        "Repeated bulk camera copies lost the first or final pixel."
      );
    }
    Check(GC.GetAllocatedBytesForCurrentThread() == before, "Bulk camera copy allocated on each frame.");
    preview.Dispose();
    Check(!preview.TryCopy(copied), "A disposed camera mapping was read through a stale pointer.");
  }

  private static void CameraDownscalingPreservesNativeAspect()
  {
    var compact = WebcamCaptureProfile.ForQuality("compact");
    var widescreen = compact.FitToSource(1920, 1080);
    Check(
      widescreen.Width == 640 && widescreen.Height == 360 && widescreen.FrameRate == 30,
      "Compact capture distorted a widescreen camera or reduced its frame rate."
    );
    var fourByThree = WebcamCaptureProfile.ForQuality("balanced").FitToSource(1600, 1200);
    Check(fourByThree.Width == 960 && fourByThree.Height == 720, "Balanced capture distorted a 4:3 camera.");
    var small = compact.FitToSource(320, 240);
    Check(small.Width == 320 && small.Height == 240, "A small camera frame was upscaled.");
    foreach (var input in new[] { (1920, 1080), (1600, 1200), (1080, 1920), (640, 360) })
    {
      CameraFrameSize size = CameraFrameSize.Fit(input.Item1, input.Item2, 320, 240);
      Check(
        size.Width <= 320 && size.Height <= 240 && size.Width % 2 == 0 && size.Height % 2 == 0,
        "Camera preview exceeded its bounded dimensions."
      );
      Check(
        Math.Abs(size.Aspect - (double)input.Item1 / input.Item2) < 0.01,
        "Camera preview changed the native aspect ratio."
      );
    }
    using var preview = new CameraPreviewBuffer(320, 240);
    var landscape = new byte[320 * 180 * 4];
    var portrait = new byte[180 * 240 * 4];
    landscape[0] = 42;
    portrait[0] = 99;
    preview.Publish(landscape, 320, 180);
    Check(
      preview.TryGetFrameSize(out CameraFrameSize frame) && frame.Width == 320 && frame.Height == 180,
      "The shared frame lost the camera's actual dimensions."
    );
    var copied = new byte[landscape.Length];
    Check(preview.TryCopy(copied) && copied[0] == 42, "A widescreen preview was not copied.");
    preview.Publish(portrait, 180, 240);
    Check(!preview.TryCopy(copied), "A resolution change copied pixels into the previous texture size.");
    Check(
      preview.TryGetFrameSize(out frame) && frame.Width == 180 && frame.Height == 240,
      "A portrait camera reused a widescreen aspect."
    );
    Check(
      preview.TryCopy(new byte[portrait.Length], out CameraFrameSize copiedSize)
        && copiedSize.Width == 180
        && copiedSize.Height == 240,
      "The copied preview lost its dimensions after a resolution change."
    );
    Check(
      new FileInfo(preview.Path).Length == CameraPreviewBuffer.HeaderSize + 320 * 240 * 4,
      "A changing camera ratio grew preview storage."
    );
    using var map = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(preview.Path, FileMode.Open);
    using var writer = map.CreateViewAccessor();
    writer.Write(0, 7L);
    writer.Write(8, 1920);
    writer.Write(12, 1080);
    writer.Write(0, 8L);
    Check(
      !preview.TryGetFrameSize(out _) && !preview.TryCopy(new byte[portrait.Length]),
      "An oversized shared frame was accepted."
    );
    Check(preview.HasFrame, "Invalid replacement metadata hid the last complete camera frame.");
    using var fullPreview = new CameraPreviewBuffer(CameraPreviewBuffer.MaxWidth, CameraPreviewBuffer.MaxHeight);
    CameraFrameSize fullSize = CameraFrameSize.Fit(1920, 1080, fullPreview.Width, fullPreview.Height);
    Check(fullSize.Width == 960 && fullSize.Height == 540, "The live preview still used the old low resolution.");
    fullPreview.Publish(new byte[fullSize.ByteCount], fullSize.Width, fullSize.Height);
    Check(fullPreview.TryCopy(new byte[fullSize.ByteCount]), "The larger preview could not be shared.");
    Check(
      new FileInfo(fullPreview.Path).Length == CameraPreviewBuffer.HeaderSize + 960 * 720 * 4,
      "Higher-quality camera previews exceeded their fixed memory budget."
    );
  }

  private static void PreviewRemainsReadyWhileTheNextFrameIsWritten()
  {
    using var preview = new CameraPreviewBuffer(32, 32);
    using var map = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(preview.Path, FileMode.Open);
    using var writer = map.CreateViewAccessor();
    writer.Write(0, 1L);
    Check(!preview.HasFrame, "An unfinished first camera frame marked the camera ready.");
    writer.Write(0, 0L);
    var original = Enumerable.Repeat((byte)42, 32 * 16 * 4).ToArray();
    preview.Publish(original, 32, 16);
    var pixels = new byte[original.Length];
    Check(preview.TryCopy(pixels) && preview.HasFrame, "The first complete camera frame was unavailable.");
    writer.Write(0, 3L);
    writer.Write(8, 16);
    writer.Write(12, 32);
    writer.WriteArray(
      CameraPreviewBuffer.HeaderSize,
      Enumerable.Repeat((byte)99, pixels.Length).ToArray(),
      0,
      pixels.Length
    );
    for (int tick = 0; tick < 100; tick++)
      Check(
        preview.HasFrame && !preview.TryCopy(pixels) && pixels[0] == 42,
        "A helper write hid or replaced the last complete camera frame."
      );
    writer.Write(0, 4L);
    Check(
      preview.TryCopy(pixels, out CameraFrameSize size)
        && size.Width == 16
        && size.Height == 32
        && pixels.All(value => value == 99),
      "A completed replacement frame had stale pixels or dimensions."
    );
    preview.Dispose();
    Check(!preview.HasFrame, "Disposing the camera preserved its ready state.");
  }

  private static void HiddenPreviewStillWarmsUpAndSkipsUnusedFrames()
  {
    using var preview = new CameraPreviewBuffer(32, 16);
    preview.SetPreviewRequested(false);
    Check(preview.ShouldPublish, "A hidden preview prevented the first readiness frame.");
    preview.Publish(new byte[32 * 16 * 4]);
    Check(preview.HasFrame && !preview.ShouldPublish, "A hidden preview kept rendering unused frames.");
    preview.SetPreviewRequested(true);
    Check(preview.ShouldPublish, "Showing the preview did not resume publication.");
    using var map = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(preview.Path, FileMode.Open);
    using var native = map.CreateViewAccessor();
    Check(native.ReadInt32(16) == 1, "The native helper could not read preview demand.");
    preview.SetPreviewRequested(false);
    Check(native.ReadInt32(16) == 0 && preview.HasFrame, "Hiding the preview lost its ready state.");
  }

  private static void PreviewReadsNeverWaitForTheProducer()
  {
    using var preview = new CameraPreviewBuffer(32, 16);
    var pixels = new byte[32 * 16 * 4];
    preview.Publish(pixels);
    Check(preview.HasFrame, "Preview fixture did not become ready.");
    object gate = typeof(CameraPreviewBuffer)
      .GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)
      .GetValue(preview);
    WhileGateIsHeld(
      gate,
      () =>
      {
        var elapsed = Stopwatch.StartNew();
        Check(
          !preview.TryGetFrameSize(out _) && !preview.TryCopy(pixels),
          "A concurrent preview write was not skipped."
        );
        preview.SetPreviewRequested(false);
        Check(
          preview.HasFrame && elapsed.Elapsed < TimeSpan.FromSeconds(1),
          "The game thread waited for a preview producer."
        );
      }
    );
    Check(preview.TryCopy(pixels), "Skipping a busy writer consumed its complete frame.");
  }

  private static void WhileGateIsHeld(object gate, Action action)
  {
    using var entered = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    Task holder = Task.Run(() =>
    {
      lock (gate)
      {
        entered.Set();
        if (!release.Wait(TimeSpan.FromSeconds(5)))
          throw new TimeoutException("Fixture gate was not released.");
      }
    });
    try
    {
      Check(entered.Wait(TimeSpan.FromSeconds(5)), "Fixture could not hold the storage or preview gate.");
      action();
    }
    finally
    {
      release.Set();
      holder.GetAwaiter().GetResult();
    }
  }

  private static void PreviewConversionPreservesColorAndOrientation()
  {
    // Top row black, bottom row white, neutral chroma. Unity textures are bottom-up.
    var yuv = new byte[] { 16, 16, 235, 235, 128, 128 };
    var rgba = new byte[16];
    CameraPreviewPixels.FromYuv420(yuv, 2, 2, rgba, 2, 2);
    Check(
      rgba[0] == 255 && rgba[1] == 255 && rgba[2] == 255 && rgba[8] == 0 && rgba[11] == 255,
      "Camera preview color or orientation changed."
    );
  }

  private static void PreviewDownscalingAveragesFineDetailWithoutAllocating()
  {
    var yuv = new byte[6 * 6 * 3 / 2];
    for (int y = 0; y < 6; y++)
    for (int x = 0; x < 6; x++)
      yuv[y * 6 + x] = (byte)((x + y) % 2 == 0 ? 16 : 235);
    for (int index = 36; index < yuv.Length; index++)
      yuv[index] = 128;
    var rgba = new byte[2 * 2 * 4];
    var converter = new CameraPreviewPixels(6, 6, 2, 2);
    converter.Convert(yuv, rgba);
    for (int pixel = 0; pixel < rgba.Length; pixel += 4)
      Check(
        rgba[pixel] >= 110
          && rgba[pixel] <= 145
          && rgba[pixel] == rgba[pixel + 1]
          && rgba[pixel] == rgba[pixel + 2]
          && rgba[pixel + 3] == 255,
        "Downscaling aliased fine camera detail instead of averaging it."
      );
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int frame = 0; frame < 100; frame++)
      converter.Convert(yuv, rgba);
    Check(GC.GetAllocatedBytesForCurrentThread() == before, "Preview conversion allocated on every frame.");
  }

  private static void DispositionCompletesOnceInEitherOrder()
  {
    for (int iteration = 0; iteration < 100; iteration++)
    {
      int completions = 0;
      bool retained = true;
      var recording = new WebcamRecording { RunId = "race" };
      var pending = new PendingWebcamDisposition(
        (actual, persist) =>
        {
          Check(ReferenceEquals(recording, actual), "The wrong recording completed.");
          retained = persist;
          Interlocked.Increment(ref completions);
        }
      );
      Parallel.Invoke(() => pending.CompleteCapture(recording), () => pending.CompleteDisposition(false));
      pending.CompleteCapture(recording);
      pending.CompleteDisposition(true);
      Check(completions == 1 && !retained, "An editor-return failure retained a video or completed twice.");
    }
  }

  private static void DispositionWaitsForActivityPersistence()
  {
    foreach (bool saved in new[] { true, false })
    {
      var committed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      using var finished = new ManualResetEventSlim();
      bool retained = !saved;
      int count = 0;
      var pending = new PendingWebcamDisposition(
        (_, persist) =>
        {
          retained = persist;
          Interlocked.Increment(ref count);
          finished.Set();
        },
        committed.Task
      );
      pending.CompleteCapture(new WebcamRecording { RunId = "pending" });
      pending.CompleteDisposition(true);
      Check(!finished.IsSet, "Video persisted before its activity transaction.");
      committed.SetResult(saved);
      Check(
        finished.Wait(TimeSpan.FromSeconds(5)) && retained == saved && count == 1,
        "Activity failure retained footage, or the video was lost after commit."
      );
      pending.CompleteDisposition(false);
    }
    var uncommitted = new TaskCompletionSource<bool>();
    using var discarded = new ManualResetEventSlim();
    var rejected = new PendingWebcamDisposition(
      (_, persist) =>
      {
        Check(!persist, "An editor-return failure retained video.");
        discarded.Set();
      },
      uncommitted.Task
    );
    rejected.CompleteCapture(new WebcamRecording());
    rejected.CompleteDisposition(false);
    Check(discarded.Wait(TimeSpan.FromSeconds(5)), "Discard waited for an unrelated pending database write.");
  }

  private static void StoreProtectsPlaybackAndEnforcesRetention(string root)
  {
    string directory = Path.Combine(root, "webcam-store");
    var store = new WebcamRecordingStore(directory);
    var first = WriteVideo(store, "first", 1024 * 1024);
    Check(store.Save(first, 4 * 1024 * 1024, 30), "Video save failed.");
    File.SetLastWriteTimeUtc(first.FilePath, DateTime.UtcNow.AddDays(-10));
    using (var lease = store.Acquire("first"))
    {
      var second = WriteVideo(store, "second", 1024 * 1024);
      Check(store.Save(second, 4 * 1024 * 1024, 30), "Second video save failed.");
      store.Cleanup(1024 * 1024, 7);
      Check(File.Exists(first.FilePath) && !File.Exists(second.FilePath), "Cleanup removed footage being replayed.");
      Check(lease.Recording.RunId == "first", "Playback metadata changed.");
    }
    store.PendingReleases.GetAwaiter().GetResult();
    store.Cleanup(1024 * 1024, 7);
    Check(!File.Exists(first.FilePath), "Expired footage was not removed after playback ended.");
    Check(store.ReserveCaptureBytes(64L * 1024 * 1024, 7) == 64L * 1024 * 1024, "Capture exceeded its storage budget.");
  }

  private static void StoreDeletionRejectsLateCapture(string root)
  {
    string directory = Path.Combine(root, "webcam-delete");
    var store = new WebcamRecordingStore(directory);
    var recording = WriteVideo(store, "../../outside", 64);
    Check(Path.GetDirectoryName(recording.FilePath) == directory, "A run id escaped webcam storage.");
    store.DeleteRun(recording.RunId);
    Check(!store.Save(recording, 1024, 7), "A deleted run retained late footage.");
    Check(!File.Exists(recording.FilePath), "Deleted footage remained on disk.");
    var active = WriteVideo(store, "active", 64);
    Check(store.Save(active, 1024, 7), "Active fixture save failed.");
    var lease = store.Acquire("active");
    store.DeleteRun("active");
    Check(File.Exists(active.FilePath), "Deletion interrupted active playback.");
    lease.Dispose();
    store.PendingReleases.GetAwaiter().GetResult();
    Check(
      !File.Exists(active.FilePath) && store.Acquire("active") == null,
      "Deleted footage was not removed after release."
    );
  }

  private static void StoreReleaseNeverWaitsForRetention(string root)
  {
    var store = new WebcamRecordingStore(Path.Combine(root, "camera-release"));
    WebcamRecording recording = WriteVideo(store, "leased", 64);
    Check(store.Save(recording, 1024, 7), "Release fixture was not saved.");
    var lease = store.Acquire(recording.RunId);
    store.DeleteRun(recording.RunId);
    object gate = typeof(WebcamRecordingStore)
      .GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)
      .GetValue(store);
    WhileGateIsHeld(
      gate,
      () =>
      {
        var elapsed = Stopwatch.StartNew();
        lease.Dispose();
        Check(elapsed.Elapsed < TimeSpan.FromSeconds(1), "VideoPlayer disposal waited for storage I/O.");
        Check(File.Exists(recording.FilePath), "The lease unpinned video before its asynchronous release.");
      }
    );
    Check(
      store.PendingReleases.Wait(TimeSpan.FromSeconds(5)) && !File.Exists(recording.FilePath),
      "An asynchronously released video was never deleted."
    );
  }

  private static void FfmpegUsesLogicalPooledFrameSize(string root)
  {
    string executable = Environment.GetEnvironmentVariable("TUFREPLAY_TEST_FFMPEG");
    if (string.IsNullOrEmpty(executable))
      executable = (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator)
        .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"))
        .FirstOrDefault(File.Exists);
    if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
    {
      Console.WriteLine("SKIP synthetic FFmpeg frame test: FFmpeg is not installed.");
      return;
    }
    var size = new CameraFrameSize(12, 8);
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("compact").FitToSource(size.Width, size.Height);
    string path = Path.Combine(root, "camera-pooled-frames.mp4");
    var capture = new FfmpegCameraRecording(
      executable,
      "pooled-frames",
      path,
      "synthetic",
      profile,
      size,
      8 * 1024 * 1024
    );
    var pixels = new byte[size.Width * size.Height * 3 / 2];
    Array.Fill(pixels, (byte)128);
    for (int frame = 0; frame < 3; frame++)
      capture.Submit(pixels, Ticks(1 + frame / 30d));
    WebcamRecording recording = capture.FinishAsync().GetAwaiter().GetResult();
    using Process decoder = FfmpegCameraProcess.Create(
      executable,
      new[] { "-v", "error", "-i", path, "-f", "rawvideo", "-pix_fmt", "yuv420p", "pipe:1" }
    );
    Check(decoder.Start(), "Synthetic camera decoder did not start.");
    decoder.BeginErrorReadLine();
    using var decoded = new MemoryStream();
    decoder.StandardOutput.BaseStream.CopyTo(decoded);
    Check(decoder.WaitForExit(5000) && decoder.ExitCode == 0, "Synthetic camera footage did not decode.");
    Check(decoded.Length == pixels.Length * 3, "Pool capacity was written as frame data, corrupting frame boundaries.");
    WebcamRecordingStore.Discard(recording);
  }

  private static void LargeCameraBuffersAreReused()
  {
    var pool =
      (ArrayPool<byte>)
        typeof(FfmpegCameraRecording)
          .GetField("FrameBuffers", BindingFlags.NonPublic | BindingFlags.Static)
          .GetValue(null);
    int bytes = 1920 * 1080 * 3 / 2;
    byte[] original = pool.Rent(bytes);
    pool.Return(original);
    byte[] reused = pool.Rent(bytes);
    try
    {
      Check(
        ReferenceEquals(original, reused),
        "1080p camera buffers exceeded the Unity/Mono pool and allocated again."
      );
    }
    finally
    {
      pool.Return(reused);
    }
  }

  private static WebcamRecording WriteVideo(WebcamRecordingStore store, string runId, long length)
  {
    string path = store.TemporaryPath(runId);
    using (var stream = File.Create(path))
    {
      stream.Write(new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 }, 0, 8);
      stream.SetLength(length);
    }
    return new WebcamRecording
    {
      RunId = runId,
      FilePath = path,
      Width = 640,
      Height = 480,
      FrameRate = 24,
      DurationUs = 1_000_000,
    };
  }

  private static void StoreRejectsCorruptTimingAndDuplicateSaves(string root)
  {
    var store = new WebcamRecordingStore(Path.Combine(root, "webcam-validation"));
    var original = WriteVideo(store, "original", 64);
    Check(store.Save(original, 1024, 7), "Original video was not saved.");
    var duplicate = WriteVideo(store, "original", 128);
    Check(!store.Save(duplicate, 1024, 7) && File.Exists(original.FilePath), "A duplicate replaced the saved video.");
    Check(new FileInfo(original.FilePath).Length == 64, "The original footage changed.");
    original.Timeline = new[]
    {
      new WebcamTimelineSegment
      {
        TimelineTimeUs = 0,
        VideoTimeUs = 0,
        GameplayRate = 0,
      },
    };
    File.WriteAllText(Path.ChangeExtension(original.FilePath, ".json"), JsonConvert.SerializeObject(original));
    bool rejected = false;
    try
    {
      using var lease = store.Acquire("original");
    }
    catch (InvalidDataException)
    {
      rejected = true;
    }
    Check(rejected, "An invalid timeline reached replay playback.");
    var oversized = WriteVideo(store, "oversized", 128L * 1024 * 1024 + 1);
    Check(
      !store.Save(oversized, 512L * 1024 * 1024, 7) && !File.Exists(oversized.FilePath),
      "A recording exceeded its per-run size limit."
    );
  }

  private static void Near(double expected, double actual) =>
    Check(Math.Abs(expected - actual) < 0.000001, $"Expected {expected}, got {actual}.");

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
