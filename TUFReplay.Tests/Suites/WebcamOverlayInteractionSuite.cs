using TUFReplay;
using TUFReplay.Webcam.Playback;

internal static class WebcamOverlayInteractionSuite
{
  public static void RunAll()
  {
    EveryBorderAndCornerCanBeGrabbed();
    OffscreenBordersAreNotMovedToTheScreenEdge();
    ResizingKeepsTheOppositeAnchorAndAspect();
    ResizingDoesNotJumpOrInvertAtItsLimits();
    DraggingUsesTheOriginalGrabOffset();
    LayoutHandlesPortraitSourcesAndScreenChanges();
    ResizingPersistsAcrossTheFullScreenHeight();
    LosingPointerCaptureFinishesAndSavesOnce();
    Console.WriteLine("TUFReplay camera overlay manipulation tests passed.");
  }

  private static void EveryBorderAndCornerCanBeGrabbed()
  {
    var rect = new WebcamOverlayBounds(100, 50, 200, 100);
    foreach (
      var point in new[]
      {
        (100d, 50d, WebcamOverlayHandle.Left | WebcamOverlayHandle.Top),
        (300d, 50d, WebcamOverlayHandle.Right | WebcamOverlayHandle.Top),
        (100d, 150d, WebcamOverlayHandle.Left | WebcamOverlayHandle.Bottom),
        (300d, 150d, WebcamOverlayHandle.Right | WebcamOverlayHandle.Bottom),
        (96d, 100d, WebcamOverlayHandle.Left),
        (304d, 100d, WebcamOverlayHandle.Right),
        (200d, 46d, WebcamOverlayHandle.Top),
        (200d, 154d, WebcamOverlayHandle.Bottom),
        (200d, 100d, WebcamOverlayHandle.Move),
        (200d, 40d, WebcamOverlayHandle.None),
      }
    )
      Check(
        WebcamOverlayInteraction.HitTest(rect, point.Item1, point.Item2) == point.Item3,
        "Camera hit target changed."
      );

    var small = new WebcamOverlayBounds(0, 0, 18, 12);
    Check(
      WebcamOverlayInteraction.HitTest(small, 9, 6) == WebcamOverlayHandle.Move,
      "Small cameras lost their move area."
    );
  }

  private static void OffscreenBordersAreNotMovedToTheScreenEdge()
  {
    var rect = new WebcamOverlayBounds(-60, -40, 200, 100);
    Check(
      WebcamOverlayInteraction.HitTest(rect, 0, 0) == WebcamOverlayHandle.Move,
      "An invisible border became a screen-edge handle."
    );
    Check(
      WebcamOverlayInteraction.HitTest(rect, 140, 0) == WebcamOverlayHandle.Right,
      "A visible offscreen camera edge could not resize."
    );
  }

  private static void ResizingKeepsTheOppositeAnchorAndAspect()
  {
    var original = new WebcamOverlayBounds(100, 80, 320, 180);
    foreach (
      WebcamOverlayHandle handle in new[]
      {
        WebcamOverlayHandle.Left,
        WebcamOverlayHandle.Right,
        WebcamOverlayHandle.Top,
        WebcamOverlayHandle.Bottom,
      }
    )
    {
      WebcamOverlayBounds resized = WebcamOverlayInteraction.Drag(original, handle, 36, 18, 100, 800);
      Near(original.Width / original.Height, resized.Width / resized.Height);
      if (handle == WebcamOverlayHandle.Left)
      {
        Near(original.X + original.Width, resized.X + resized.Width);
        Near(original.Y + original.Height / 2, resized.Y + resized.Height / 2);
      }
      else if (handle == WebcamOverlayHandle.Right)
      {
        Near(original.X, resized.X);
        Near(original.Y + original.Height / 2, resized.Y + resized.Height / 2);
      }
      else if (handle == WebcamOverlayHandle.Top)
      {
        Near(original.Y + original.Height, resized.Y + resized.Height);
        Near(original.X + original.Width / 2, resized.X + resized.Width / 2);
      }
      else
      {
        Near(original.Y, resized.Y);
        Near(original.X + original.Width / 2, resized.X + resized.Width / 2);
      }
    }
    foreach (WebcamOverlayHandle horizontal in new[] { WebcamOverlayHandle.Left, WebcamOverlayHandle.Right })
    foreach (WebcamOverlayHandle vertical in new[] { WebcamOverlayHandle.Top, WebcamOverlayHandle.Bottom })
    {
      WebcamOverlayBounds resized = WebcamOverlayInteraction.Drag(original, horizontal | vertical, 32, 18, 100, 800);
      Near(original.Width / original.Height, resized.Width / resized.Height);
      Near(
        horizontal == WebcamOverlayHandle.Left ? original.X + original.Width : original.X,
        horizontal == WebcamOverlayHandle.Left ? resized.X + resized.Width : resized.X
      );
      Near(
        vertical == WebcamOverlayHandle.Top ? original.Y + original.Height : original.Y,
        vertical == WebcamOverlayHandle.Top ? resized.Y + resized.Height : resized.Y
      );
    }
  }

  private static void ResizingDoesNotJumpOrInvertAtItsLimits()
  {
    var original = new WebcamOverlayBounds(-50, 50, 200, 100);
    WebcamOverlayBounds unchanged = WebcamOverlayInteraction.Drag(
      original,
      WebcamOverlayHandle.Left | WebcamOverlayHandle.Top,
      0,
      0,
      100,
      500
    );
    Near(original.X, unchanged.X);
    Near(original.Y, unchanged.Y);
    Near(original.Width, unchanged.Width);
    WebcamOverlayBounds smallest = WebcamOverlayInteraction.Drag(
      original,
      WebcamOverlayHandle.Left | WebcamOverlayHandle.Top,
      10000,
      10000,
      100,
      500
    );
    Near(100, smallest.Width);
    Near(original.X + original.Width, smallest.X + smallest.Width);
    Near(original.Y + original.Height, smallest.Y + smallest.Height);
    WebcamOverlayBounds largest = WebcamOverlayInteraction.Drag(
      original,
      WebcamOverlayHandle.Left,
      -10000,
      0,
      100,
      500
    );
    Near(500, largest.Width);
    Near(original.X + original.Width, largest.X + largest.Width);
  }

  private static void DraggingUsesTheOriginalGrabOffset()
  {
    var original = new WebcamOverlayBounds(100, 50, 200, 100);
    WebcamOverlayBounds moved = WebcamOverlayInteraction.Drag(original, WebcamOverlayHandle.Move, -125, 37, 100, 500);
    Near(-25, moved.X);
    Near(87, moved.Y);
    Near(original.Width, moved.Width);
    Near(original.Height, moved.Height);
  }

  private static void LayoutHandlesPortraitSourcesAndScreenChanges()
  {
    var settings = new TUFReplaySetting { WebcamOverlayWidth = 0.3 };
    foreach (var size in new[] { (1920d, 1080d), (320d, 240d), (600d, 1200d) })
    foreach (double aspect in new[] { 16d / 9, 0.3, 5d })
    {
      WebcamOverlayLayout.MoveTo(settings, -50, 40, size.Item1, size.Item2, aspect);
      WebcamOverlayBounds rect = WebcamOverlayLayout.Get(settings, size.Item1, size.Item2, aspect);
      Near(-50, rect.X);
      Near(40, rect.Y);
      Near(aspect, rect.Width / rect.Height);
    }
  }

  private static void ResizingPersistsAcrossTheFullScreenHeight()
  {
    var settings = new TUFReplaySetting { WebcamOverlayWidth = 0.3 };
    var original = new WebcamOverlayBounds(25, -200, 300, 900);
    foreach (double height in new[] { 999d, 1000d, 1001d })
    {
      WebcamOverlayBounds resized = WebcamOverlayInteraction.Drag(
        original,
        WebcamOverlayHandle.Top,
        0,
        900 - height,
        100,
        500
      );
      WebcamOverlayLayout.Apply(settings, resized, 1000, 1000, 1d / 3);
      settings.Normalize();
      WebcamOverlayBounds persisted = WebcamOverlayLayout.Get(settings, 1000, 1000, 1d / 3);
      Near(height, persisted.Height);
      Near(original.Y + original.Height, persisted.Y + persisted.Height);
      Near(resized.X, persisted.X);
    }
    WebcamOverlayLayout.MoveTo(settings, -50, 40, 1000, 1000, 1d / 3);
    WebcamOverlayBounds moved = WebcamOverlayLayout.Get(settings, 1000, 1000, 1d / 3);
    Near(-50, moved.X);
    Near(40, moved.Y);
    WebcamOverlayBounds smallerScreen = WebcamOverlayLayout.Get(settings, 500, 500, 1d / 3);
    Near(-25, smallerScreen.X);
    Near(20, smallerScreen.Y);
  }

  private static void LosingPointerCaptureFinishesAndSavesOnce()
  {
    var original = new WebcamOverlayBounds(100, 50, 200, 100);
    var gesture = new WebcamOverlayGesture();
    foreach (
      var interruption in new[]
      {
        (false, true, 1000, 800, 2d),
        (true, false, 1000, 800, 2d),
        (true, true, 1200, 800, 2d),
        (true, true, 1000, 800, 1.5d),
      }
    )
    {
      gesture.Begin(original, WebcamOverlayHandle.Move, 150, 75, 1000, 800);
      WebcamOverlayBounds moved = gesture.Move(175, 90);
      Near(125, moved.X);
      Near(65, moved.Y);
      Check(!gesture.NeedsFinish(true, true, 1000, 800, 2), "An unchanged focused pointer lost capture.");
      Check(
        gesture.NeedsFinish(
          interruption.Item1,
          interruption.Item2,
          interruption.Item3,
          interruption.Item4,
          interruption.Item5
        ),
        "An interrupted camera gesture kept capture."
      );
      Check(gesture.Finish() && !gesture.Active, "The changed camera position was not saved on interruption.");
      Check(!gesture.Finish(), "An interrupted camera gesture saved twice.");
    }
    gesture.Begin(original, WebcamOverlayHandle.Move, 150, 75, 1000, 800);
    Check(!gesture.Finish(), "A click without a drag rewrote camera settings.");
    gesture.Begin(original, WebcamOverlayHandle.Left, 103, 75, 1000, 800);
    WebcamOverlayBounds unchanged = gesture.Move(103, 75);
    Near(original.X, unchanged.X);
    Near(original.Width, unchanged.Width);
    Check(!gesture.Finish(), "Grabbing a padded edge moved the camera before pointer motion.");
  }

  private static void Near(double expected, double actual) =>
    Check(Math.Abs(expected - actual) < 0.000001, $"Expected {expected}, got {actual}.");

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
