using System;
using System.Diagnostics;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Settings;
using TUFReplay.Webcam.Diagnostics;
using TUFReplay.Webcam.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TUFReplay.Webcam.Playback;

public sealed class WebcamReplayOverlay
  : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler,
    IDragHandler,
    IEndDragHandler
{
  private const float EdgeWidth = 8;
  private static WebcamReplayOverlay _visibleOverlay;
  private VideoPlayer _video;
  private Texture _preview;
  private double _aspect;
  private readonly WebcamOverlayGesture _gesture = new WebcamOverlayGesture();
  private Canvas _canvas;
  private RawImage _image;
  private RectTransform _rect;
  private bool _cursorRegistered;
  private bool _showFrame;

  public bool ShowFrame
  {
    get => _showFrame;
    set
    {
      if (_showFrame == value)
        return;
      _showFrame = value;
      if (!value)
        FinishGesture();
      RefreshSurface();
    }
  }

  public static bool IsConsumingDragInput
  {
    get
    {
      WebcamReplayOverlay overlay = _visibleOverlay;
      if (overlay == null || !overlay.ShowFrame || !Application.isFocused || Cursor.lockState != CursorLockMode.None)
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
    _preview = null;
    _aspect = aspect;
    RegisterCursor();
    CreateSurface();
    RefreshSurface();
  }

  public void InitializePreview(Texture texture, double aspect)
  {
    _video = null;
    _preview = texture;
    _aspect = aspect;
    RegisterCursor();
    CreateSurface();
    RefreshSurface();
  }

  private void CreateSurface()
  {
    if (_canvas != null)
      return;
    var root = new GameObject("CameraCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
    root.SetActive(false);
    root.transform.SetParent(transform, false);
    _canvas = root.GetComponent<Canvas>();
    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    _canvas.overrideSorting = true;
    // Keep the timeline and camera-check modal (32000) above the video.
    _canvas.sortingOrder = 31999;

    var image = new GameObject("CameraFrame", typeof(RectTransform), typeof(RawImage));
    image.transform.SetParent(root.transform, false);
    _rect = image.GetComponent<RectTransform>();
    _rect.anchorMin = _rect.anchorMax = new Vector2(0, 1);
    _rect.pivot = new Vector2(0, 1);
    _image = image.GetComponent<RawImage>();
    _image.maskable = false;
    // Expand the pointer target without stretching or adding a second image.
    _image.raycastPadding = new Vector4(-EdgeWidth, -EdgeWidth, -EdgeWidth, -EdgeWidth);
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
    long started = Stopwatch.GetTimestamp();
    bool visible = false;
    try
    {
      UpdateCursor();
      visible = RefreshSurface();
    }
    finally
    {
      CameraRenderDiagnostics.RecordOverlayUpdate(Stopwatch.GetTimestamp() - started, visible, _video != null);
    }
  }

  private void UpdateCursor()
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

  private bool RefreshSurface()
  {
    Texture texture = Frame;
    bool visible = isActiveAndEnabled && ShowFrame && texture != null && _canvas != null;
    if (!visible)
    {
      if (_canvas != null && _canvas.gameObject.activeSelf)
        _canvas.gameObject.SetActive(false);
      if (_visibleOverlay == this)
        _visibleOverlay = null;
      return false;
    }
    _visibleOverlay = this;
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    WebcamOverlayBounds bounds = Bounds(settings);
    var position = new Vector2((float)bounds.X, -(float)bounds.Y);
    var size = new Vector2((float)bounds.Width, (float)bounds.Height);
    WebcamTextureCoordinates coordinates = WebcamCropRect.Get(settings).TextureCoordinates(settings.WebcamMirror);
    var uv = new Rect((float)coordinates.X, (float)coordinates.Y, (float)coordinates.Width, (float)coordinates.Height);
    // Reuse the same quad and texture. A newly uploaded camera frame does not
    // require a Canvas mesh/layout rebuild when its dimensions are unchanged.
    if (_image.texture != texture)
      _image.texture = texture;
    if (_image.uvRect != uv)
      _image.uvRect = uv;
    if (_rect.anchoredPosition != position)
      _rect.anchoredPosition = position;
    if (_rect.sizeDelta != size)
      _rect.sizeDelta = size;
    if (!_canvas.gameObject.activeSelf)
      _canvas.gameObject.SetActive(true);
    return true;
  }

  private void OnDisable()
  {
    if (_visibleOverlay == this)
      _visibleOverlay = null;
    FinishGesture();
    WebcamOverlayCursor.Release(this);
    if (_canvas != null)
      _canvas.gameObject.SetActive(false);
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

  public void OnInitializePotentialDrag(PointerEventData input) => input.useDragThreshold = false;

  public void OnPointerDown(PointerEventData input)
  {
    long started = Stopwatch.GetTimestamp();
    try
    {
      if (
        input.button != PointerEventData.InputButton.Left
        || !ShowFrame
        || Frame == null
        || !Application.isFocused
        || Cursor.lockState != CursorLockMode.None
        || _gesture.Active
      )
        return;
      Vector2 pointer = PointerPosition(input.position);
      if (!IsInsideScreen(pointer))
        return;
      WebcamOverlayBounds bounds = Bounds(TUFReplaySettingStore.Current);
      WebcamOverlayHandle handle = WebcamOverlayInteraction.HitTest(bounds, pointer.x, pointer.y, EdgeWidth);
      if (handle == WebcamOverlayHandle.None)
        return;
      _gesture.Begin(bounds, handle, pointer.x, pointer.y, Screen.width, Screen.height);
      input.useDragThreshold = false;
      input.Use();
    }
    finally
    {
      CameraRenderDiagnostics.Record(CameraRenderPhase.OverlayInput, Stopwatch.GetTimestamp() - started);
    }
  }

  public void OnDrag(PointerEventData input)
  {
    long started = Stopwatch.GetTimestamp();
    try
    {
      if (input.button != PointerEventData.InputButton.Left || !_gesture.Active)
        return;
      if (
        !ShowFrame
        || Frame == null
        || _gesture.NeedsFinish(
          Application.isFocused,
          Input.GetMouseButton(0),
          Screen.width,
          Screen.height,
          WebcamCropRect.Get(TUFReplaySettingStore.Current).AspectRatio(_aspect)
        )
      )
      {
        FinishGesture();
        return;
      }
      Vector2 pointer = PointerPosition(input.position);
      WebcamOverlayBounds updated = _gesture.Move(pointer.x, pointer.y);
      WebcamOverlayLayout.Apply(
        TUFReplaySettingStore.Current,
        updated,
        _gesture.ScreenWidth,
        _gesture.ScreenHeight,
        _gesture.Aspect
      );
      RefreshSurface();
      input.Use();
    }
    finally
    {
      CameraRenderDiagnostics.Record(CameraRenderPhase.OverlayInput, Stopwatch.GetTimestamp() - started);
    }
  }

  public void OnPointerUp(PointerEventData input) => EndPointer(input);

  public void OnEndDrag(PointerEventData input) => EndPointer(input);

  private void EndPointer(PointerEventData input)
  {
    long started = Stopwatch.GetTimestamp();
    try
    {
      if (input.button != PointerEventData.InputButton.Left || !_gesture.Active)
        return;
      FinishGesture();
      input.Use();
    }
    finally
    {
      CameraRenderDiagnostics.Record(CameraRenderPhase.OverlayInput, Stopwatch.GetTimestamp() - started);
    }
  }

  private void FinishGesture()
  {
    if (!_gesture.Active)
      return;
    bool changed = _gesture.Finish();
    if (changed)
      SavePosition();
  }

  private static Vector2 PointerPosition() => PointerPosition(Input.mousePosition);

  private static Vector2 PointerPosition(Vector2 pointer) => new Vector2(pointer.x, Screen.height - pointer.y);

  private static bool IsInsideScreen(Vector2 pointer) =>
    pointer.x >= 0 && pointer.x <= Screen.width && pointer.y >= 0 && pointer.y <= Screen.height;

  private static void SavePosition()
  {
    try
    {
      TUFReplaySettingStore.Save();
      TUFReplay.Composition.FeatureRegistry.WebcamRecording?.NotifyStateChanged();
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
