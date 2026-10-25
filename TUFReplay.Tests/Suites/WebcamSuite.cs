using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Tracking;
using TUFReplay.Composition;
using TUFReplay.Microphone.Models;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Playback;
using TUFReplay.Shared.Capture;
using TUFReplay.Shared.Settings;
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
    StoreDownloadCancellationReleasesItsLease(root);
    TimelineSkipsRecordedPausesAndHandlesRateChanges();
    AttachingAnEarlierCameraFrameKeepsReplaySynchronized();
    CameraTailSurvivesMicrophoneCompletionUntilEditorOrRetry();
    FailureCameraTailUsesRealTime();
    CameraCompletionKeepsHealthyCaptureArmed(root);
    EncoderFailureDoesNotReopenHealthyCamera(root);
    BrowserSnapshotsDoNotConsumeGameFrames();
    CameraInstallationWaitDoesNotStartARun(root);
    CameraRunCannotUseAnInvalidatedBackend(root);
    WonTailAndLatencyHaveTheSameConventionAsMicrophone();
    FfmpegDiscoverySeparatesAudioFromVideo();
    SettingsRejectInvalidValuesAndKeepTheCameraOffByDefault();
    LegacyTotalStorageSettingDoesNotBecomeARecordingLimit(root);
    OverlayAllowsOffscreenPlacementWithoutMargins();
    SharedPreviewFramesAreBoundedAndNeverTorn();
    MaximumPreviewFramesUseExactAllocationFreeCopies();
    TimelineRejectsInvalidAnchors();
    PreviewRemainsReadyWhileTheNextFrameIsWritten();
    HiddenPreviewStillWarmsUpAndSkipsUnusedFrames();
    PreviewReadsNeverWaitForTheProducer();
    CameraDownscalingPreservesNativeAspect();
    DispositionCompletesOnceInEitherOrder();
    DispositionWaitsForActivityPersistence();
    StoreProtectsPlaybackAndEnforcesRetention(root);
    StoreKeepsForeverAndExportsWithoutLoadingVideo(root);
    CleanupNotifiesActivityWhenVideosOrRetentionChange(root);
    StoreDeletionRejectsLateCapture(root);
    StoreRejectsCorruptTimingAndDuplicateSaves(root);
    StoreReleaseNeverWaitsForRetention(root);
    FfmpegUsesLogicalFrameSize(root);
    NativeRelayPublishesManagedReadablePreviews(root);
    PreparedFfmpegWriterKeepsIdleTimeOutOfVideo(root);
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
        SetCameraField(camera, "_runBackend", backend);
        SetCameraField(camera, "_armed", true);
        SetCameraField(camera, "_recording", true);
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

  private static string FindTestFfmpeg() =>
    Environment.GetEnvironmentVariable("TUFREPLAY_CAMERA_TEST_FFMPEG")
    ?? Environment.GetEnvironmentVariable("TUFREPLAY_TEST_FFMPEG")
    ?? (Environment.GetEnvironmentVariable("PATH") ?? "")
      .Split(Path.PathSeparator)
      .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"))
      .FirstOrDefault(File.Exists);

  private static void PreparedFfmpegWriterKeepsIdleTimeOutOfVideo(string root)
  {
    string executable = FindTestFfmpeg();
    if (string.IsNullOrWhiteSpace(executable) || !SyntheticCameraCapture.IsAvailable)
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
    byte[] pixels = new byte[640 * 360 * 3 / 2];
    Array.Fill(pixels, (byte)128, 640 * 360, pixels.Length - 640 * 360);
    bool finalizing = false;
    using (var capture = new SyntheticCameraCapture(640, 360))
    {
      try
      {
        // Preparing the encoder must not create footage for the idle interval.
        Thread.Sleep(100);
        Check(!File.Exists(destination), "The idle camera writer used a run destination before attachment.");
        // A cached camera frame from before input capture must be rejected.
        capture.Emit(pixels, Ticks(9.99));
        Check(capture.WaitReady(), "The synthetic camera never became ready.");
        capture.Now = Ticks(10);
        recording.Attach("synthetic-run", destination, WebcamRecordingStore.MaximumCaptureBytes, Ticks(10));
        recording.Connect(capture);
        for (int frame = 0; frame < 12; frame++)
        {
          capture.Emit(pixels, Ticks(10 + frame / 30d));
          Thread.Sleep(35);
        }
        Check(
          SpinWait.SpinUntil(() => recording.RelayStats.WrittenFrames == 12, TimeSpan.FromSeconds(5)),
          "The relay did not stream every run frame."
        );
        finalizing = true;
        recording.Seal();
        WebcamRecording result = recording.FinishAsync().GetAwaiter().GetResult();
        Check(result.RunId == "synthetic-run" && result.FilePath == destination, "Camera attachment lost its run.");
        Check(result.CaptureStartTimestampTicks == Ticks(10), "The prepared encoder changed the first frame clock.");
        Check(
          result.DurationUs > 0 && result.DurationUs <= 400_000,
          "The prepared encoder reported an invalid duration: " + result.DurationUs
        );
        Check(File.Exists(destination) && !File.Exists(standby), "Camera footage kept the standby destination.");
        Check(CountDecodedFrames(executable, destination) == 12, "The prepared MP4 saved idle time or lost frames.");
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
      unused.Attach(
        "empty-retry",
        Path.Combine(root, "camera-empty.mp4.partial"),
        WebcamRecordingStore.MaximumCaptureBytes,
        Ticks(20)
      );
      capture.Now = Ticks(20);
      unused.Connect(capture);
      capture.EmitAndWaitCaptured(pixels, Ticks(19.5));
      unused.Seal();
      Check(
        unused.FinishAsync().GetAwaiter().GetResult() == null,
        "A retry before the first camera frame produced a video or camera error."
      );
      Check(!File.Exists(unusedPath), "An idle camera encoder leaked its temporary file.");
    }
    Console.WriteLine("TUFReplay prepared FFmpeg camera encoder integration test passed (no camera access).");
  }

  private static int CountDecodedFrames(string executable, string path)
  {
    using var decoder = FfmpegCameraProcess.Create(
      executable,
      new[] { "-v", "error", "-nostats", "-i", path, "-an", "-progress", "pipe:1", "-f", "null", "-" }
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
    Check(decoder.ExitCode == 0, "The camera MP4 could not be decoded: " + errors.GetAwaiter().GetResult());
    string last = progress
      .GetAwaiter()
      .GetResult()
      .Split('\n')
      .Where(line => line.StartsWith("frame="))
      .LastOrDefault();
    return last == null ? 0 : int.Parse(last.Trim().Substring(6), CultureInfo.InvariantCulture);
  }

  private static void FfmpegUsesLogicalFrameSize(string root)
  {
    string executable = FindTestFfmpeg();
    if (string.IsNullOrEmpty(executable) || !File.Exists(executable) || !SyntheticCameraCapture.IsAvailable)
    {
      Console.WriteLine("SKIP synthetic FFmpeg frame test: FFmpeg or the camera relay is unavailable.");
      return;
    }
    var size = new CameraFrameSize(12, 8);
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("compact").FitToSource(size.Width, size.Height);
    string path = Path.Combine(root, "camera-logical-frames.mp4");
    var recording = new FfmpegCameraRecording(
      executable,
      "logical-frames",
      path,
      "synthetic",
      profile,
      size,
      8 * 1024 * 1024
    );
    var pixels = new byte[size.Width * size.Height * 3 / 2];
    Array.Fill(pixels, (byte)128);
    using (var capture = new SyntheticCameraCapture(size.Width, size.Height))
    {
      capture.Emit(pixels, Ticks(0.5));
      Check(capture.WaitReady(), "The synthetic camera never became ready.");
      capture.Now = Ticks(1);
      recording.Attach("logical-frames", path + ".saved", 8 * 1024 * 1024, Ticks(1));
      recording.Connect(capture);
      for (int frame = 0; frame < 3; frame++)
      {
        long before = recording.RelayStats.WrittenFrames;
        capture.Emit(pixels, Ticks(1 + frame / 30d));
        Check(
          SpinWait.SpinUntil(() => recording.RelayStats.WrittenFrames > before, TimeSpan.FromSeconds(5)),
          "The relay did not stream a synthetic frame."
        );
      }
      recording.Seal();
    }
    WebcamRecording saved = recording.FinishAsync().GetAwaiter().GetResult();
    using Process decoder = FfmpegCameraProcess.Create(
      executable,
      new[] { "-v", "error", "-i", saved.FilePath, "-f", "rawvideo", "-pix_fmt", "yuv420p", "pipe:1" }
    );
    Check(decoder.Start(), "Synthetic camera decoder did not start.");
    decoder.BeginErrorReadLine();
    using var decoded = new MemoryStream();
    decoder.StandardOutput.BaseStream.CopyTo(decoded);
    Check(decoder.WaitForExit(5000) && decoder.ExitCode == 0, "Synthetic camera footage did not decode.");
    Check(decoded.Length == pixels.Length * 3, "Frame boundaries were corrupted in the encoder stream.");
    WebcamRecordingStore.Discard(saved);
  }

  // The native relay writes the shared preview mapping that Unity and the
  // companion read through CameraPreviewBuffer.
  private static void NativeRelayPublishesManagedReadablePreviews(string root)
  {
    if (!SyntheticCameraCapture.IsAvailable)
      return;
    using var buffer = new CameraPreviewBuffer(CameraPreviewBuffer.MaxWidth, CameraPreviewBuffer.MaxHeight);
    using var capture = new SyntheticCameraCapture(1280, 720, buffer.Path);
    var pixels = new byte[1280 * 720 * 3 / 2];
    Array.Fill(pixels, (byte)235, 0, 1280 * 720);
    Array.Fill(pixels, (byte)128, 1280 * 720, pixels.Length - 1280 * 720);
    capture.Emit(pixels, Ticks(1));
    Check(capture.WaitReady(), "The synthetic camera never became ready.");
    Check(
      SpinWait.SpinUntil(() => buffer.HasFrame, TimeSpan.FromSeconds(5)),
      "The native relay never published its first preview frame."
    );
    Check(
      buffer.TryGetFrameSize(out CameraFrameSize size) && size.Width == 960 && size.Height == 540,
      "The native preview lost the camera aspect ratio."
    );
    var rgba = new byte[size.ByteCount];
    Check(buffer.TryCopy(rgba), "Managed code could not read the native preview frame.");
    Check(rgba[0] == 255 && rgba[1] == 255 && rgba[2] == 255 && rgba[3] == 255, "The native preview colors changed.");
    buffer.SetPreviewRequested(false);
    long published = capture.State.PreviewsPublished;
    capture.EmitAndWaitCaptured(pixels, Ticks(2));
    Check(capture.State.PreviewsPublished == published, "Previews kept converting without demand.");
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
      SetCameraField(camera, "_runBackend", backend);
      SetCameraField(camera, "_armed", true);
      SetCameraField(camera, "_recording", true);
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

  private static void EncoderFailureDoesNotReopenHealthyCamera(string root)
  {
    WithEnabledCameraSettings(
      root,
      () =>
      {
        var backend = new CompletedCameraBackend(null) { FailNextEnd = true };
        var camera = new WebcamRecordingFeature();
        SetCameraField(camera, "_active", true);
        SetCameraField(camera, "_armed", true);
        SetCameraField(camera, "_backend", backend);
        typeof(WebcamRecordingFeature)
          .GetProperty(nameof(WebcamRecordingFeature.Store))
          .SetValue(camera, new WebcamRecordingStore(Path.Combine(root, "camera-encoder-recovery")));
        for (int attempt = 0; attempt < 2; attempt++)
        {
          Check(camera.BeginRun("encoder-retry-" + attempt), "Encoder failure rejected the next run.");
          camera.EndRun(null, result => Check(result == null, "Synthetic encoder returned a video."));
          WaitForCameraWork(camera);
          Check(camera.IsReady && backend.DisarmCalls == 0, "An encoder failure reopened the camera.");
        }
        Check(
          backend.BeginCalls == 2 && backend.EndCalls == 2 && camera.GetState().Error == null,
          "Successful recording did not clear the previous encoder error."
        );
      }
    );
  }

  private static void BrowserSnapshotsDoNotConsumeGameFrames()
  {
    using var buffer = new CameraPreviewBuffer(2, 2);
    byte[] pixels = { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255 };
    buffer.Publish(pixels);
    buffer.SetPreviewRequested(false);
    Check(!buffer.ShouldPublish, "Hidden preview continued publishing without demand.");
    buffer.RequestBrowserPreview();
    buffer.SetPreviewRequested(false);
    Check(buffer.ShouldPublish, "Game preview hiding cancelled browser demand.");
    var snapshot = new byte[16];
    Check(buffer.TryCopySnapshot(snapshot, out var size), "Browser snapshot missed a complete frame.");
    byte[] bitmap = CameraPreviewBitmap.Encode(snapshot, size, out var target);
    Check(
      target.Width == 2 && target.Height == 2 && bitmap.Length == 70,
      "Preview bitmap dimensions or padding are incorrect."
    );
    Check(
      bitmap[54] == 0 && bitmap[56] == 255 && bitmap[62] == 255 && bitmap[64] == 0,
      "The bitmap lost bottom-up orientation or RGBA to BGR conversion."
    );
    byte[] reused = Enumerable.Repeat((byte)0xAB, bitmap.Length).ToArray();
    byte[] destination = reused;
    CameraPreviewBitmap.EncodeInto(snapshot, size, ref destination, out _);
    Check(
      ReferenceEquals(destination, reused) && destination.SequenceEqual(bitmap),
      "Reusing a preview bitmap left stale bytes or allocated a new frame."
    );
    Check(buffer.TryCopy(new byte[16]), "Browser snapshot consumed the game preview sequence.");
    Check(
      buffer.TryCopySnapshot(snapshot, out _) && !buffer.TryCopy(new byte[16]),
      "Independent preview readers interfered."
    );
    typeof(CameraPreviewBuffer)
      .GetField("_browserRequestedUntil", BindingFlags.NonPublic | BindingFlags.Instance)
      .SetValue(buffer, 0L);
    buffer.SetPreviewRequested(false);
    Check(!buffer.ShouldPublish, "Expired browser demand kept publishing pixels.");
  }

  private static void SetCameraField(WebcamRecordingFeature camera, string name, object value) =>
    typeof(WebcamRecordingFeature)
      .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
      .SetValue(camera, value);

  private static void CameraInstallationWaitDoesNotStartARun(string root)
  {
    WithEnabledCameraSettings(
      root,
      () =>
      {
        var camera = new WebcamRecordingFeature();
        var installation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetCameraField(camera, "_active", true);
        SetCameraField(camera, "_arming", true);
        SetCameraField(camera, "_work", installation.Task);
        typeof(WebcamRecordingFeature)
          .GetProperty(nameof(WebcamRecordingFeature.Store))
          .SetValue(camera, new WebcamRecordingStore(Path.Combine(root, "camera-install-wait")));
        try
        {
          Check(!camera.BeginRun("during-install"), "An installing camera accepted a recording without a backend.");
          int callbacks = 0;
          camera.EndRun(
            null,
            result =>
            {
              Check(result == null, "An installing camera returned a recording.");
              callbacks++;
            }
          );
          Check(callbacks == 1, "Ending a skipped camera run waited for FFmpeg installation.");
          Check(!camera.GetState().CaptureLocked, "An installation wait locked camera recording settings.");
        }
        finally
        {
          installation.TrySetResult(true);
          WaitForCameraWork(camera);
        }
        Check(camera.GetState().Error == null, "Playing during installation produced a camera error.");
        var backend = new CompletedCameraBackend(null);
        SetCameraField(camera, "_backend", backend);
        // Creating the backend is not enough; its first-frame warmup must finish.
        Check(!camera.BeginRun("during-warmup"), "A warming camera accepted a recording before its first frame.");
        SetCameraField(camera, "_arming", false);
        SetCameraField(camera, "_armed", true);
        Check(camera.BeginRun("after-install"), "Camera readiness did not recover after installation.");
        int completions = 0;
        camera.EndRun(null, _ => Interlocked.Increment(ref completions));
        WaitForCameraWork(camera);
        Check(backend.BeginCalls == 1 && backend.EndCalls == 1, "The next run did not use the prepared camera.");
        Check(completions == 1 && !camera.GetState().CaptureLocked, "The next camera run did not finish cleanly.");
      }
    );
  }

  private static void CameraRunCannotUseAnInvalidatedBackend(string root)
  {
    WithEnabledCameraSettings(
      root,
      () =>
      {
        var camera = new WebcamRecordingFeature();
        var backend = new CompletedCameraBackend(null);
        var queuedWork = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetCameraField(camera, "_active", true);
        SetCameraField(camera, "_armed", true);
        SetCameraField(camera, "_backend", backend);
        SetCameraField(camera, "_work", queuedWork.Task);
        typeof(WebcamRecordingFeature)
          .GetProperty(nameof(WebcamRecordingFeature.Store))
          .SetValue(camera, new WebcamRecordingStore(Path.Combine(root, "camera-stale-begin")));
        int callbacks = 0;
        try
        {
          Check(camera.BeginRun("queued-run"), "A ready camera rejected a run.");
          camera.Disarm();
          camera.EndRun(
            null,
            result =>
            {
              Check(result == null, "An invalidated camera returned a recording.");
              Interlocked.Increment(ref callbacks);
            }
          );
        }
        finally
        {
          queuedWork.TrySetResult(true);
          WaitForCameraWork(camera);
        }
        Check(backend.BeginCalls == 0 && backend.EndCalls == 0, "A queued run used an invalidated camera backend.");
        Check(backend.DisarmCalls == 1, "Invalidating a queued run did not disarm the camera.");
        Check(callbacks == 1 && !camera.GetState().CaptureLocked, "Invalidating a run leaked its completion or lock.");
        Check(camera.GetState().Error == null, "Invalidating a queued run produced a camera error.");
      }
    );
  }

  private static void WaitForCameraWork(WebcamRecordingFeature camera)
  {
    Task work = (Task)
      typeof(WebcamRecordingFeature).GetField("_work", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(camera);
    Check(work.Wait(TimeSpan.FromSeconds(5)), "Camera work did not finish.");
  }

  private static void WithEnabledCameraSettings(string root, Action action)
  {
    var current = typeof(TUFReplaySettingStore).GetProperty(nameof(TUFReplaySettingStore.Current));
    var path = typeof(TUFReplaySettingStore).GetField("_path", BindingFlags.NonPublic | BindingFlags.Static);
    object previousSettings = current.GetValue(null);
    object previousPath = path.GetValue(null);
    try
    {
      TUFReplaySettingStore.Initialize(Path.Combine(root, "camera-lifecycle-settings.json"));
      Main.Settings.WebcamEnabled = true;
      action();
    }
    finally
    {
      current.SetValue(null, previousSettings);
      path.SetValue(null, previousPath);
    }
  }

  private sealed class CompletedCameraBackend : IWebcamCaptureBackend
  {
    private readonly WebcamRecording _recording;
    public bool FailNextEnd;
    public int BeginCalls;
    public int EndCalls;
    public int DisarmCalls;
    public CameraPreviewBuffer Preview => null;
    public string CaptureWarning => null;

    public CompletedCameraBackend(WebcamRecording recording) => _recording = recording;

    public Task<WebcamRecording> EndAsync()
    {
      EndCalls++;
      if (FailNextEnd)
      {
        FailNextEnd = false;
        throw new IOException("Synthetic encoder failure");
      }
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

    public Task BeginAsync(string runId, string path, long maxBytes, long startTimestampTicks = 0)
    {
      BeginCalls++;
      return Task.CompletedTask;
    }

    public Task KeepEncoderReadyAsync(long maxBytes) => Task.CompletedTask;

    public Task ReleaseEncoderAsync() => Task.CompletedTask;

    public void Dispose() { }
  }

  private static void TimelineRejectsInvalidAnchors()
  {
    var timeline = new WebcamCaptureTimeline();
    timeline.Observe(new CaptureTimelineAnchor(Ticks(1), 0, double.NaN));
    timeline.Observe(new CaptureTimelineAnchor(0, 0, 1));
    Check(!timeline.First.HasValue, "Invalid anchors must not start the camera timeline.");
    timeline.Observe(new CaptureTimelineAnchor(Ticks(2), 1_000_000, 1));
    timeline.Observe(new CaptureTimelineAnchor(Ticks(3), 0, 1));
    var recording = new WebcamRecording { CaptureStartTimestampTicks = Ticks(1) };
    Check(timeline.TryApplyTo(recording), "A valid anchor must still synchronize camera video.");
    Check(recording.Timeline.Length == 1, "Backward anchors must not alter the camera timeline.");
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
        "{\"offsetMs\":null}",
        "{\"recordingLimitMb\":1}",
        "{\"storageLimitMb\":512}",
        "{\"retentionDays\":-1}",
        "{\"otherSetting\":true}",
      }
    )
      Check(
        !WebcamSettingsPatchParser.TryParse(JObject.Parse(invalid), out _),
        "Invalid settings were accepted: " + invalid
      );
    Check(
      WebcamSettingsPatchParser.TryParse(
        JObject.Parse("{\"offsetMs\":120,\"overlayX\":0,\"mirror\":true}"),
        out var patch
      ),
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
    settings.WebcamDeviceId = "camera-to-preserve";
    patch.ApplyTo(settings);
    Check(settings.WebcamDeviceId == "camera-to-preserve", "An omitted typed deviceId reset the selected camera.");
    Check(
      WebcamSettingsPatchParser.TryParse(JObject.Parse("{\"deviceId\":null}"), out var defaultCamera),
      "An explicit typed null deviceId was rejected."
    );
    defaultCamera.ApplyTo(settings);
    Check(settings.WebcamDeviceId == null, "An explicit typed null did not restore the default camera.");
    settings.WebcamOverlayX = double.NaN;
    settings.WebcamOverlayWidth = double.PositiveInfinity;
    settings.Normalize();
    Near(1, settings.WebcamOverlayX);
    Near(0.22, settings.WebcamOverlayWidth);
    Check(settings.WebcamRecordingLimitMb == 128, "The default per-recording limit changed.");
    Check(
      WebcamSettingsPatchParser.TryParse(
        JObject.Parse("{\"recordingLimitMb\":2048,\"retentionDays\":0}"),
        out var forever
      ),
      "Forever retention or a larger per-recording limit was rejected."
    );
    forever.ApplyTo(settings);
    settings.Normalize();
    Check(
      settings.WebcamRecordingLimitMb == 2048 && settings.WebcamRetentionDays == 0,
      "The per-recording limit or forever retention was not preserved."
    );
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

  private static void LegacyTotalStorageSettingDoesNotBecomeARecordingLimit(string root)
  {
    string path = Path.Combine(root, "legacy-webcam-settings.json");
    File.WriteAllText(path, "{\"WebcamStorageLimitMb\":8192,\"WebcamRetentionDays\":0}");
    TUFReplaySetting settings = TUFReplaySetting.Load(path);
    Check(
      settings.WebcamRecordingLimitMb == 128 && settings.WebcamRetentionDays == 0,
      "The removed total-storage budget was reused as a per-recording limit."
    );
    settings.Save(path);
    Check(
      !File.ReadAllText(path).Contains("WebcamStorageLimitMb"),
      "The removed total-storage setting was saved again."
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
    Check(corner.WebcamQuality == "balanced", "A new camera must default to Balanced quality.");
    WebcamOverlayBounds right = WebcamOverlayLayout.Get(corner, 1920, 1080, 16d / 9);
    Near(1920, right.X + right.Width);
    Near(1080, right.Y + right.Height);
    Check(
      WebcamSettingsPatchParser.TryParse(
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
      profile.Width == 1920 && profile.Height == 1080 && profile.FrameRate == 60 && profile.BitRate == 6_000_000,
      "Quality profile changed."
    );
  }

  private static void SharedPreviewFramesAreBoundedAndNeverTorn()
  {
    using var preview = new CameraPreviewBuffer(32, 24);
    var pixels = new byte[preview.ByteCount];
    Check(!preview.HasFrame && !preview.TryCopy(pixels), "An uninitialized camera frame was displayed.");
    using var map = OpenProducerMapping(preview.Path);
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
    var settings = new TUFReplaySetting { WebcamQuality = "compact" };
    settings.Normalize();
    Check(settings.WebcamQuality == "compact", "Saved Compact quality must remain selected.");
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
    using var map = OpenProducerMapping(preview.Path);
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

  // Opens the preview file the way a native producer does: sharing access with
  // the managed mapping, which Windows otherwise refuses.
  private static System.IO.MemoryMappedFiles.MemoryMappedFile OpenProducerMapping(string path)
  {
    var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
    return System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(
      file,
      null,
      0,
      System.IO.MemoryMappedFiles.MemoryMappedFileAccess.ReadWrite,
      HandleInheritability.None,
      leaveOpen: false
    );
  }

  private static void PreviewRemainsReadyWhileTheNextFrameIsWritten()
  {
    using var preview = new CameraPreviewBuffer(32, 32);
    using var map = OpenProducerMapping(preview.Path);
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
    using var map = OpenProducerMapping(preview.Path);
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
    first.CreatedAtUtc = DateTime.UtcNow.AddDays(-10);
    Check(store.Save(first, 1024 * 1024, 30), "Video save failed.");
    using (var lease = store.Acquire("first"))
    {
      var second = WriteVideo(store, "second", 1024 * 1024);
      Check(store.Save(second, 1024 * 1024, 7), "Second video save failed.");
      Check(File.Exists(first.FilePath) && File.Exists(second.FilePath), "A new recording evicted older footage.");
      store.Cleanup(7);
      Check(
        File.Exists(first.FilePath) && File.Exists(second.FilePath),
        "Cleanup removed leased or unexpired footage."
      );
      Check(lease.Recording.RunId == "first", "Playback metadata changed.");
    }
    store.PendingReleases.GetAwaiter().GetResult();
    store.Cleanup(7);
    Check(!File.Exists(first.FilePath), "Expired footage was not removed after playback ended.");
    Check(
      File.Exists(Path.Combine(directory, Path.GetFileName(store.TemporaryPath("second")).Replace(".partial", ""))),
      "Retention deleted a video that had not expired."
    );
  }

  private static void StoreKeepsForeverAndExportsWithoutLoadingVideo(string root)
  {
    string directory = Path.Combine(root, "camera-forever");
    var store = new WebcamRecordingStore(directory);
    WebcamRecording expired = WriteVideo(store, "global-forever", 64);
    expired.CreatedAtUtc = DateTime.UtcNow.AddDays(-60);
    Check(store.Save(expired, 1024, 7), "Global forever fixture was not saved.");
    var expiredRun = new RunRecord { Id = expired.RunId };
    store.PopulateMetadata(new[] { expiredRun }, 7);
    Check(expiredRun.CameraRecordingBytes == 0, "Expired legacy-compatible metadata remained available.");
    store.Cleanup(0);
    Check(File.Exists(expired.FilePath), "Forever retention removed an old video.");
    Check(store.KeepPermanently(expired.RunId), "An existing video could not be kept permanently.");
    WebcamRecording forever = WriteVideo(store, "new-forever", 64);
    forever.CreatedAtUtc = DateTime.UtcNow.AddDays(-90);
    Check(store.Save(forever, 1024, 0), "A forever video was not saved.");
    WebcamRecording extended = WriteVideo(store, "extended-retention", 64);
    extended.CreatedAtUtc = DateTime.UtcNow.AddDays(-10);
    Check(store.Save(extended, 1024, 7), "Extended retention fixture was not saved.");
    store.Cleanup(30);
    var extendedRun = new RunRecord { Id = extended.RunId };
    store.PopulateMetadata(new[] { extendedRun }, 30);
    Check(
      File.Exists(extended.FilePath) && extendedRun.CameraRecordingBytes == 64,
      "Increasing retention did not extend an existing temporary recording."
    );
    var reopened = new WebcamRecordingStore(directory);
    reopened.Cleanup(1);
    Check(File.Exists(expired.FilePath) && File.Exists(forever.FilePath), "Permanent videos expired after reopening.");
    var run = new RunRecord { Id = expired.RunId };
    reopened.PopulateMetadata(new[] { run }, 7);
    Check(
      run.CameraRecordingBytes == 64 && run.CameraRecordingPermanent && run.CameraRecordingExpiresAtUtc == null,
      "Camera management metadata did not report permanent storage."
    );
    using var exported = new MemoryStream();
    reopened.WriteTo(expired.RunId, exported, 64);
    Check(exported.ToArray().SequenceEqual(File.ReadAllBytes(expired.FilePath)), "Export changed camera video bytes.");
    using (var lease = reopened.Acquire(expired.RunId))
    {
      Check(reopened.DeleteRun(expired.RunId), "Camera-only deletion did not report the existing video.");
      Check(
        reopened.GetMetadata(expired.RunId) == null && File.Exists(expired.FilePath),
        "Deleted leased video remained visible or was physically removed too early."
      );
    }
    reopened.PendingReleases.GetAwaiter().GetResult();
    Check(!File.Exists(expired.FilePath), "Deleted camera video survived its final lease.");
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

  private static void CleanupNotifiesActivityWhenVideosOrRetentionChange(string root)
  {
    WithEnabledCameraSettings(
      root,
      () =>
      {
        Main.Settings.WebcamRetentionDays = 7;
        var store = new WebcamRecordingStore(Path.Combine(root, "camera-cleanup-events"));
        var camera = new WebcamRecordingFeature();
        typeof(WebcamRecordingFeature).GetProperty(nameof(WebcamRecordingFeature.Store)).SetValue(camera, store);
        var cleanup = typeof(WebcamRecordingFeature).GetMethod(
          "Cleanup",
          BindingFlags.Instance | BindingFlags.NonPublic
        );
        int notifications = 0;
        void OnChanged(string runId) => notifications++;
        ActivityChanges.Changed += OnChanged;
        try
        {
          cleanup.Invoke(camera, new object[] { false });
          Check(notifications == 0, "An unchanged retention scan invalidated play history.");
          WebcamRecording expired = WriteVideo(store, "cleanup-expired", 64);
          expired.CreatedAtUtc = DateTime.UtcNow.AddDays(-10);
          Check(store.Save(expired, 1024, 7), "Cleanup event fixture was not saved.");
          cleanup.Invoke(camera, new object[] { false });
          Check(notifications == 1 && !File.Exists(expired.FilePath), "Expired footage did not refresh play history.");
          Main.Settings.WebcamRetentionDays = 0;
          cleanup.Invoke(camera, new object[] { true });
          Check(notifications == 2, "Changing to forever did not refresh derived recording metadata.");
        }
        finally
        {
          ActivityChanges.Changed -= OnChanged;
        }
      }
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

  private static void StoreDownloadCancellationReleasesItsLease(string root)
  {
    var store = new WebcamRecordingStore(Path.Combine(root, "webcam-download-cancellation"));
    var recording = WriteVideo(store, "cancelled-download", 256 * 1024);
    Check(store.Save(recording, 512 * 1024, 7), "The cancellation recording was not saved.");
    using var cancellation = new CancellationTokenSource();
    using var destination = new CancelAfterWriteStream(cancellation);
    bool cancelled = false;
    try
    {
      store.WriteTo(recording.RunId, destination, 256 * 1024, cancellation.Token);
    }
    catch (OperationCanceledException)
    {
      cancelled = true;
    }
    Check(
      cancelled && destination.Length == 65536,
      "A disconnected camera export continued reading the full recording."
    );
    store.PendingReleases.GetAwaiter().GetResult();
    Check(store.DeleteRun(recording.RunId), "Cancelled export lost its recording.");
    Check(!File.Exists(recording.FilePath), "Cancelled export retained its file lease.");
  }

  private sealed class CancelAfterWriteStream : MemoryStream
  {
    private readonly CancellationTokenSource _cancellation;

    public CancelAfterWriteStream(CancellationTokenSource cancellation)
    {
      _cancellation = cancellation;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      base.Write(buffer, offset, count);
      _cancellation.Cancel();
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
    var oversized = WriteVideo(store, "oversized", 1025);
    Check(
      !store.Save(oversized, 1024, 7) && !File.Exists(oversized.FilePath),
      "A recording exceeded its per-run size limit."
    );
    var larger = WriteVideo(store, "larger-per-recording-limit", 129L * 1024 * 1024);
    Check(store.Save(larger, 256L * 1024 * 1024, 7), "The old hard-coded 128 MB limit still rejected a valid video.");
  }

  private static void Near(double expected, double actual) =>
    Check(Math.Abs(expected - actual) < 0.000001, $"Expected {expected}, got {actual}.");

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
