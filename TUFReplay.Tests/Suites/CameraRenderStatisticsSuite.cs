using Newtonsoft.Json.Linq;
using TUFReplay.Webcam.Diagnostics;

internal static class CameraRenderStatisticsSuite
{
  public static void RunAll()
  {
    MultipleOverlayUpdatesShareOneGameFrame();
    WindowResetRetainsTheUnfinishedFrame();
    RecordingMeasurementsDoesNotAllocate();
    Console.WriteLine("TUFReplay camera render measurement tests passed.");
  }

  private static void MultipleOverlayUpdatesShareOneGameFrame()
  {
    var statistics = new CameraRenderStatistics(10_000);
    statistics.BeginFrame(10, 1000);
    statistics.SetContext(true, true, true, true, false);
    statistics.RecordOverlayUpdate(6, true, false);
    statistics.RecordOverlayUpdate(9, true, false);
    statistics.Record(CameraRenderPhase.OverlayInput, 4);
    statistics.Record(CameraRenderPhase.PreviewRead, 1);
    statistics.Record(CameraRenderPhase.PreviewRead, 2);
    statistics.Record(CameraRenderPhase.SharedCopy, 2);
    statistics.RecordUpload(960, 540, true);
    statistics.BeginFrame(11, 1100);
    statistics.SetContext(false, false, true, false, false);
    statistics.BeginFrame(12, 1200);

    Check(statistics.CompletedFrames == 2, "Overlay updates inflated the game-frame count.");
    Near(0.02, statistics.WindowSeconds);
    Check(statistics.Phase(CameraRenderPhase.OverlayUpdate).Count == 2, "Overlay update counts were lost.");
    Near(1.5, statistics.Phase(CameraRenderPhase.OverlayUpdate).TotalMs);
    Near(0.95, statistics.UiPerFrame.MeanMs);
    Near(1.9, statistics.UiPerFrame.MaxMs);
    Check(statistics.UiPerVisibleFrame.Count == 1, "Hidden frames inflated visible-frame cost.");
    Near(1.9, statistics.UiPerVisibleFrame.MeanMs);
    Near(0.4, statistics.Phase(CameraRenderPhase.OverlayInput).TotalMs);

    JObject snapshot = JObject.FromObject(statistics.DiagnosticState());
    Near(100, (double)snapshot["gameFps"]);
    Check((int)snapshot["contextFrames"]["cameraEnabled"] == 1, "Disabled frames lost their baseline state.");
    Check((int)snapshot["uGui"]["liveVisibleUpdates"] == 2, "Visible overlay updates were omitted.");
    Check((int)snapshot["uGui"]["pointerInput"]["count"] == 1, "Pointer input was not separated from updates.");
    Check((int)snapshot["preview"]["readAttempts"]["count"] == 2, "Preview read attempts were lost.");
    Check((int)snapshot["preview"]["completedCopies"]["count"] == 1, "Empty reads were counted as copies.");
    Check((int)snapshot["preview"]["uploadedFrames"] == 1, "Uploads were counted per game frame.");
    Check((int)snapshot["preview"]["lastUploadedWidth"] == 960, "Uploaded dimensions were lost.");
  }

  private static void WindowResetRetainsTheUnfinishedFrame()
  {
    var statistics = new CameraRenderStatistics(1000);
    statistics.BeginFrame(1, 1000);
    statistics.RecordOverlayUpdate(2, true, false);
    statistics.BeginFrame(2, 1010);
    statistics.RecordOverlayUpdate(3, true, true);
    object previous = statistics.DiagnosticState();
    statistics.ResetWindow();
    statistics.BeginFrame(3, 1020);

    Check(statistics.CompletedFrames == 1, "Window reset duplicated or dropped a frame.");
    Near(3, statistics.UiPerFrame.TotalMs);
    JObject current = JObject.FromObject(statistics.DiagnosticState());
    Check((int)current["uGui"]["replayVisibleUpdates"] == 1, "Unfinished replay updates were dropped.");
    JObject detached = JObject.FromObject(previous);
    Near(2, (double)detached["uGui"]["updates"]["totalMs"]);
    Check((int)detached["uGui"]["replayVisibleUpdates"] == 0, "Worker snapshots referenced mutable counters.");
  }

  private static void RecordingMeasurementsDoesNotAllocate()
  {
    var statistics = new CameraRenderStatistics(1000);
    for (int frame = 0; frame < 100; frame++)
    {
      statistics.BeginFrame(frame, frame * 10);
      statistics.RecordOverlayUpdate(1, true, false);
    }
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int frame = 100; frame < 2100; frame++)
    {
      statistics.BeginFrame(frame, frame * 10);
      statistics.RecordOverlayUpdate(1, true, false);
      statistics.Record(CameraRenderPhase.OverlayInput, 1);
      statistics.Record(CameraRenderPhase.SharedCopy, 1);
      statistics.RecordUpload(960, 540, false);
    }
    Check(GC.GetAllocatedBytesForCurrentThread() == before, "Per-frame render diagnostics allocated memory.");
  }

  private static void Near(double expected, double actual) =>
    Check(Math.Abs(expected - actual) < 0.000001, $"Expected {expected}, received {actual}.");

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
