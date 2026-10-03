using System;

namespace TUFReplay.Webcam.Playback;

[Flags]
public enum WebcamOverlayHandle
{
  None = 0,
  Left = 1,
  Right = 2,
  Top = 4,
  Bottom = 8,
  Move = 16,
}

public static class WebcamOverlayInteraction
{
  public static WebcamOverlayHandle HitTest(WebcamOverlayBounds bounds, double x, double y, double edgeWidth = 8)
  {
    double edge = Math.Max(1, Math.Min(edgeWidth, Math.Min(bounds.Width, bounds.Height) / 3));
    double right = bounds.X + bounds.Width;
    double bottom = bounds.Y + bounds.Height;
    if (x < bounds.X - edge || x > right + edge || y < bounds.Y - edge || y > bottom + edge)
      return WebcamOverlayHandle.None;

    WebcamOverlayHandle handle = WebcamOverlayHandle.None;
    if (Math.Abs(x - bounds.X) <= edge)
      handle |= WebcamOverlayHandle.Left;
    else if (Math.Abs(x - right) <= edge)
      handle |= WebcamOverlayHandle.Right;
    if (Math.Abs(y - bounds.Y) <= edge)
      handle |= WebcamOverlayHandle.Top;
    else if (Math.Abs(y - bottom) <= edge)
      handle |= WebcamOverlayHandle.Bottom;
    if (handle != WebcamOverlayHandle.None)
      return handle;
    return x >= bounds.X && x <= right && y >= bounds.Y && y <= bottom
      ? WebcamOverlayHandle.Move
      : WebcamOverlayHandle.None;
  }

  public static WebcamOverlayBounds Drag(
    WebcamOverlayBounds start,
    WebcamOverlayHandle handle,
    double deltaX,
    double deltaY,
    double minWidth,
    double maxWidth
  )
  {
    if (handle == WebcamOverlayHandle.Move)
      return new WebcamOverlayBounds(start.X + deltaX, start.Y + deltaY, start.Width, start.Height);
    if (handle == WebcamOverlayHandle.None)
      return start;

    double aspect = start.Width / start.Height;
    int horizontal =
      (handle & WebcamOverlayHandle.Left) != 0 ? -1
      : (handle & WebcamOverlayHandle.Right) != 0 ? 1
      : 0;
    int vertical =
      (handle & WebcamOverlayHandle.Top) != 0 ? -1
      : (handle & WebcamOverlayHandle.Bottom) != 0 ? 1
      : 0;
    double change;
    if (horizontal != 0 && vertical != 0)
    {
      // Project the pointer delta onto the aspect-preserving corner trajectory.
      double inverseAspect = 1 / aspect;
      change = (deltaX * horizontal + deltaY * vertical * inverseAspect) / (1 + inverseAspect * inverseAspect);
    }
    else
      change = horizontal != 0 ? deltaX * horizontal : deltaY * vertical * aspect;

    double width = Math.Max(minWidth, Math.Min(maxWidth, start.Width + change));
    double height = width / aspect;
    double x =
      horizontal < 0 ? start.X + start.Width - width
      : horizontal > 0 ? start.X
      : start.X + (start.Width - width) / 2;
    double y =
      vertical < 0 ? start.Y + start.Height - height
      : vertical > 0 ? start.Y
      : start.Y + (start.Height - height) / 2;
    return new WebcamOverlayBounds(x, y, width, height);
  }
}
