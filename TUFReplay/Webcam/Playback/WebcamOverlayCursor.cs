using System;
using HarmonyLib;
using UnityEngine;

namespace TUFReplay.Webcam.Playback;

internal enum WebcamCursorShape
{
  None,
  Move,
  Horizontal,
  Vertical,
  NorthwestSoutheast,
  NortheastSouthwest,
}

internal static class WebcamOverlayCursor
{
  private static readonly Texture2D[] Textures = new Texture2D[6];
  private static WebcamReplayOverlay _owner;
  private static WebcamCursorShape _appliedShape;
  private static Texture2D _externalTexture;
  private static Vector2 _externalHotspot;
  private static CursorMode _externalMode;
  private static bool _settingCursor;
  private static bool _previousVisible;
  private static int _instances;

  public static void Register() => _instances++;

  public static void Unregister(WebcamReplayOverlay owner)
  {
    Release(owner);
    if (--_instances > 0)
      return;
    _instances = 0;
    for (int index = 1; index < Textures.Length; index++)
    {
      if (Textures[index] != null)
        UnityEngine.Object.Destroy(Textures[index]);
      Textures[index] = null;
    }
  }

  public static void Show(WebcamReplayOverlay owner, WebcamOverlayHandle handle)
  {
    WebcamCursorShape shape = ShapeFor(handle);
    if (shape == WebcamCursorShape.None || !Application.isFocused || Cursor.lockState != CursorLockMode.None)
    {
      Release(owner);
      return;
    }
    if (_owner != owner)
    {
      if (_owner != null)
        Release(_owner);
      _owner = owner;
      _previousVisible = Cursor.visible;
    }
    Cursor.visible = true;
    if (_appliedShape == shape)
      return;
    Texture2D texture = Textures[(int)shape];
    if (texture == null)
      Textures[(int)shape] = texture = CreateTexture(shape);
    SetCursor(texture, new Vector2(16, 16), CursorMode.Auto);
    _appliedShape = shape;
  }

  public static void Release(WebcamReplayOverlay owner)
  {
    if (_owner != owner || _owner == null)
      return;
    _owner = null;
    SetCursor(_externalTexture, _externalHotspot, _externalMode);
    Cursor.visible = _previousVisible;
    _appliedShape = WebcamCursorShape.None;
  }

  // Cursor exposes no getter for its texture. Remember requests from the game and
  // other mods so leaving the camera restores their cursor instead of replacing it.
  public static void ObserveExternalCursor(Texture2D texture, Vector2 hotspot, CursorMode mode)
  {
    if (_settingCursor)
      return;
    _externalTexture = texture;
    _externalHotspot = hotspot;
    _externalMode = mode;
    _appliedShape = WebcamCursorShape.None;
  }

  private static void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode mode)
  {
    _settingCursor = true;
    try
    {
      Cursor.SetCursor(texture, hotspot, mode);
    }
    finally
    {
      _settingCursor = false;
    }
  }

  private static WebcamCursorShape ShapeFor(WebcamOverlayHandle handle)
  {
    if (handle == WebcamOverlayHandle.Move)
      return WebcamCursorShape.Move;
    bool horizontal = (handle & (WebcamOverlayHandle.Left | WebcamOverlayHandle.Right)) != 0;
    bool vertical = (handle & (WebcamOverlayHandle.Top | WebcamOverlayHandle.Bottom)) != 0;
    if (horizontal && vertical)
      return ((handle & WebcamOverlayHandle.Left) != 0) == ((handle & WebcamOverlayHandle.Top) != 0)
        ? WebcamCursorShape.NorthwestSoutheast
        : WebcamCursorShape.NortheastSouthwest;
    return horizontal ? WebcamCursorShape.Horizontal
      : vertical ? WebcamCursorShape.Vertical
      : WebcamCursorShape.None;
  }

  private static Texture2D CreateTexture(WebcamCursorShape shape)
  {
    var pixels = new Color32[32 * 32];
    if (shape == WebcamCursorShape.Move)
    {
      Arrow(pixels, 5, 16, 27, 16);
      Arrow(pixels, 16, 5, 16, 27);
    }
    else if (shape == WebcamCursorShape.Horizontal)
      Arrow(pixels, 5, 16, 27, 16);
    else if (shape == WebcamCursorShape.Vertical)
      Arrow(pixels, 16, 5, 16, 27);
    else if (shape == WebcamCursorShape.NorthwestSoutheast)
      Arrow(pixels, 7, 25, 25, 7);
    else
      Arrow(pixels, 7, 7, 25, 25);

    var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
    {
      name = "TUFReplay camera " + shape + " cursor",
      hideFlags = HideFlags.HideAndDontSave,
      filterMode = FilterMode.Bilinear,
      wrapMode = TextureWrapMode.Clamp,
    };
    texture.SetPixels32(pixels);
    texture.Apply(false, false);
    return texture;
  }

  private static void Arrow(Color32[] pixels, double startX, double startY, double endX, double endY)
  {
    double length = Math.Sqrt((endX - startX) * (endX - startX) + (endY - startY) * (endY - startY));
    double dx = (endX - startX) / length;
    double dy = (endY - startY) / length;
    Line(pixels, startX, startY, endX, endY);
    Line(pixels, startX, startY, startX + 5 * dx - 4 * dy, startY + 5 * dy + 4 * dx);
    Line(pixels, startX, startY, startX + 5 * dx + 4 * dy, startY + 5 * dy - 4 * dx);
    Line(pixels, endX, endY, endX - 5 * dx - 4 * dy, endY - 5 * dy + 4 * dx);
    Line(pixels, endX, endY, endX - 5 * dx + 4 * dy, endY - 5 * dy - 4 * dx);
  }

  private static void Line(Color32[] pixels, double startX, double startY, double endX, double endY)
  {
    double dx = endX - startX;
    double dy = endY - startY;
    double lengthSquared = dx * dx + dy * dy;
    for (int y = 0; y < 32; y++)
    for (int x = 0; x < 32; x++)
    {
      double fraction = Math.Max(0, Math.Min(1, ((x - startX) * dx + (y - startY) * dy) / lengthSquared));
      double offsetX = x - startX - fraction * dx;
      double offsetY = y - startY - fraction * dy;
      double distanceSquared = offsetX * offsetX + offsetY * offsetY;
      int index = y * 32 + x;
      if (distanceSquared <= 0.8)
        pixels[index] = new Color32(255, 255, 255, 255);
      else if (distanceSquared <= 4 && pixels[index].a == 0)
        pixels[index] = new Color32(24, 24, 24, 255);
    }
  }
}

[HarmonyPatch(
  typeof(Cursor),
  nameof(Cursor.SetCursor),
  new[] { typeof(Texture2D), typeof(Vector2), typeof(CursorMode) }
)]
internal static class WebcamCursorRestorePatch
{
  private static void Prefix(Texture2D texture, Vector2 hotspot, CursorMode cursorMode) =>
    WebcamOverlayCursor.ObserveExternalCursor(texture, hotspot, cursorMode);
}
