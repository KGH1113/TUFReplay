using System;

namespace TUFReplay.Webcam.Playback;

// Owns pointer tracking independently of IMGUI, so capture can finish consistently
// on mouse-up, focus loss, a hidden frame, or a changed game-window geometry.
public sealed class WebcamOverlayGesture
{
  private WebcamOverlayBounds _start;
  private double _pointerX;
  private double _pointerY;
  private int _screenWidth;
  private int _screenHeight;
  private bool _changed;

  public WebcamOverlayHandle Handle { get; private set; }
  public bool Active => Handle != WebcamOverlayHandle.None;
  public double Aspect => _start.Width / _start.Height;
  public int ScreenWidth => _screenWidth;
  public int ScreenHeight => _screenHeight;

  public void Begin(
    WebcamOverlayBounds bounds,
    WebcamOverlayHandle handle,
    double pointerX,
    double pointerY,
    int screenWidth,
    int screenHeight
  )
  {
    _start = bounds;
    Handle = handle;
    _pointerX = pointerX;
    _pointerY = pointerY;
    _screenWidth = Math.Max(1, screenWidth);
    _screenHeight = Math.Max(1, screenHeight);
    _changed = false;
  }

  public WebcamOverlayBounds Move(double pointerX, double pointerY)
  {
    WebcamOverlayBounds bounds = WebcamOverlayInteraction.Drag(
      _start,
      Handle,
      pointerX - _pointerX,
      pointerY - _pointerY,
      _screenWidth * 0.1,
      _screenWidth * 0.5
    );
    if (Active)
      _changed |=
        Math.Abs(bounds.X - _start.X) > 0.01
        || Math.Abs(bounds.Y - _start.Y) > 0.01
        || Math.Abs(bounds.Width - _start.Width) > 0.01;
    return bounds;
  }

  public bool NeedsFinish(bool focused, bool pressed, int screenWidth, int screenHeight, double aspect) =>
    Active
    && (
      !focused
      || !pressed
      || screenWidth != _screenWidth
      || screenHeight != _screenHeight
      || Math.Abs(aspect - Aspect) > 0.000001
    );

  public bool Finish()
  {
    bool changed = Active && _changed;
    Handle = WebcamOverlayHandle.None;
    _changed = false;
    return changed;
  }
}
