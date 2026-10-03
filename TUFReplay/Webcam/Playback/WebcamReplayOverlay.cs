using System;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Settings;
using TUFReplay.Webcam.Models;
using UnityEngine;
using UnityEngine.Video;

namespace TUFReplay.Webcam.Playback;

public sealed class WebcamReplayOverlay : MonoBehaviour
{
  private static WebcamReplayOverlay _visibleOverlay;
  private VideoPlayer _video;
  private Texture _preview;
  private double _aspect;
  private readonly WebcamOverlayGesture _gesture = new WebcamOverlayGesture();
  private int _controlId;
  private bool _cursorRegistered;
  private bool _showFrame;

  public bool ShowFrame
  {
    get => _showFrame;
    set
    {
      _showFrame = value;
      if (!value)
        FinishGesture();
    }
  }

  public static bool IsConsumingDragInput
  {
    get
    {
      WebcamReplayOverlay overlay = _visibleOverlay;
      if (overlay == null || !overlay.ShowFrame || !Application.isFocused)
        return false;
      if (overlay._gesture.Active)
        return true;
      if (!Input.GetMouseButton(0))
        return false;
      Vector2 pointer = PointerPosition();
      return IsInsideScreen(pointer)
        && WebcamOverlayInteraction.HitTest(overlay.Bounds(TUFReplaySettingStore.Current), pointer.x, pointer.y)
          != WebcamOverlayHandle.None;
    }
  }

  public void Initialize(VideoPlayer video, double aspect)
  {
    _video = video;
    _aspect = aspect;
    RegisterCursor();
  }

  public void InitializePreview(Texture texture, double aspect)
  {
    _preview = texture;
    _aspect = aspect;
    RegisterCursor();
  }

  private void RegisterCursor()
  {
    if (_cursorRegistered)
      return;
    WebcamOverlayCursor.Register();
    _cursorRegistered = true;
  }

  private Texture Frame => _video != null ? _video.texture : _preview;

  private WebcamOverlayBounds Bounds(TUFReplaySetting settings) =>
    WebcamOverlayLayout.Get(settings, Screen.width, Screen.height, WebcamCropRect.Get(settings).AspectRatio(_aspect));

  private void LateUpdate()
  {
    if (!ShowFrame || Frame == null || !Application.isFocused || Cursor.lockState != CursorLockMode.None)
    {
      FinishGesture();
      WebcamOverlayCursor.Release(this);
      return;
    }
    if (
      _gesture.NeedsFinish(
        Application.isFocused,
        Input.GetMouseButton(0),
        Screen.width,
        Screen.height,
        WebcamCropRect.Get(TUFReplaySettingStore.Current).AspectRatio(_aspect)
      )
    )
      FinishGesture();

    Vector2 pointer = PointerPosition();
    WebcamOverlayHandle hover = _gesture.Handle;
    if (hover == WebcamOverlayHandle.None && IsInsideScreen(pointer))
      hover = WebcamOverlayInteraction.HitTest(Bounds(TUFReplaySettingStore.Current), pointer.x, pointer.y);
    WebcamOverlayCursor.Show(this, hover);
  }

  private void OnGUI()
  {
    Texture texture = Frame;
    if (!ShowFrame || texture == null)
    {
      if (_visibleOverlay == this)
        _visibleOverlay = null;
      return;
    }
    _visibleOverlay = this;
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    WebcamOverlayBounds bounds = Bounds(settings);
    var rect = new Rect((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
    int id = GUIUtility.GetControlID(GetInstanceID(), FocusType.Passive);
    int previousDepth = GUI.depth;
    GUI.depth = -100;
    try
    {
      WebcamTextureCoordinates coordinates = WebcamCropRect.Get(settings).TextureCoordinates(settings.WebcamMirror);
      GUI.DrawTextureWithTexCoords(
        rect,
        texture,
        new Rect((float)coordinates.X, (float)coordinates.Y, (float)coordinates.Width, (float)coordinates.Height)
      );
      HandleInput(bounds, settings, id);
    }
    finally
    {
      GUI.depth = previousDepth;
    }
  }

  private void OnDisable()
  {
    if (_visibleOverlay == this)
      _visibleOverlay = null;
    FinishGesture();
    WebcamOverlayCursor.Release(this);
  }

  private void OnDestroy()
  {
    if (_cursorRegistered)
    {
      WebcamOverlayCursor.Unregister(this);
      _cursorRegistered = false;
    }
  }

  private void OnApplicationFocus(bool focused)
  {
    if (focused)
      return;
    FinishGesture();
    WebcamOverlayCursor.Release(this);
  }

  private void HandleInput(WebcamOverlayBounds bounds, TUFReplaySetting settings, int id)
  {
    Event input = Event.current;
    if (
      input.type == EventType.MouseDown
      && input.button == 0
      && Application.isFocused
      && Cursor.lockState == CursorLockMode.None
      && GUIUtility.hotControl == 0
      && IsInsideScreen(input.mousePosition)
    )
    {
      WebcamOverlayHandle handle = WebcamOverlayInteraction.HitTest(
        bounds,
        input.mousePosition.x,
        input.mousePosition.y
      );
      if (handle == WebcamOverlayHandle.None)
        return;
      _gesture.Begin(bounds, handle, input.mousePosition.x, input.mousePosition.y, Screen.width, Screen.height);
      _controlId = id;
      GUIUtility.hotControl = id;
      input.Use();
    }
    else if (_gesture.Active && GUIUtility.hotControl != _controlId)
      FinishGesture();
    else if (input.type == EventType.MouseDrag && _gesture.Active)
    {
      WebcamOverlayBounds updated = _gesture.Move(input.mousePosition.x, input.mousePosition.y);
      WebcamOverlayLayout.Apply(settings, updated, _gesture.ScreenWidth, _gesture.ScreenHeight, _gesture.Aspect);
      input.Use();
    }
    else if (input.type == EventType.MouseUp && input.button == 0 && _gesture.Active)
    {
      FinishGesture();
      input.Use();
    }
  }

  private void FinishGesture()
  {
    if (!_gesture.Active)
      return;
    bool changed = _gesture.Finish();
    // Release while hiding/destroying: a disabled overlay may never receive
    // another OnGUI callback.
    if (_controlId != 0 && GUIUtility.hotControl == _controlId)
      GUIUtility.hotControl = 0;
    _controlId = 0;
    if (changed)
      SavePosition();
  }

  private static Vector2 PointerPosition()
  {
    Vector3 pointer = Input.mousePosition;
    return new Vector2(pointer.x, Screen.height - pointer.y);
  }

  private static bool IsInsideScreen(Vector2 pointer) =>
    pointer.x >= 0 && pointer.x <= Screen.width && pointer.y >= 0 && pointer.y <= Screen.height;

  private static void SavePosition()
  {
    try
    {
      TUFReplaySettingStore.Save();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Webcam/Layout", exception);
      ReplayTimelineHud.ShowNotificationToast(
        "Camera position could not be saved",
        "The position applies for this session. Check available disk space and try again."
      );
    }
  }
}
