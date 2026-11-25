using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TUFReplay.Replay.Rendering.Webcam;

/// <summary>
/// The webcam on its own canvas, which OrbitRender captures with the game's HUD at the video's
/// size. Anchored by fractions of the video, it lands on the exact pixels OrbitRender drew it on.
/// </summary>
internal sealed class RenderWebcamCanvas : IDisposable
{
  // Above the game's HUD, as the camera was. Key viewer videos still go on top, in FFmpeg.
  private const int SortingOrder = 30000;

  private readonly GameObject _root;
  private readonly RawImage _image;
  private readonly Texture2D _texture;

  public RenderWebcamCanvas(int videoWidth, int videoHeight, RenderWebcamLayout layout)
  {
    _root = new GameObject("TUFReplay Render Webcam", typeof(RectTransform), typeof(Canvas));
    Object.DontDestroyOnLoad(_root);
    Canvas = _root.GetComponent<Canvas>();
    Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    Canvas.sortingOrder = SortingOrder;

    var frame = new GameObject("Webcam", typeof(RectTransform), typeof(RawImage));
    frame.transform.SetParent(_root.transform, false);
    var rect = frame.GetComponent<RectTransform>();
    rect.anchorMin = new Vector2(layout.X / (float)videoWidth, 1 - (layout.Y + layout.Height) / (float)videoHeight);
    rect.anchorMax = new Vector2((layout.X + layout.Width) / (float)videoWidth, 1 - layout.Y / (float)videoHeight);
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;

    // An sRGB colour texture: Unity converts it as the project's colour space needs, so the
    // camera's gamma is applied once.
    _texture = new Texture2D(layout.Width, layout.Height, TextureFormat.RGBA32, false, false)
    {
      name = "TUFReplay Render Webcam",
      wrapMode = TextureWrapMode.Clamp,
      filterMode = FilterMode.Bilinear,
    };
    _image = frame.GetComponent<RawImage>();
    _image.texture = _texture;
    _image.raycastTarget = false;
    _image.maskable = false;
    // FFmpeg's rows run from the top, Unity's from the bottom.
    _image.uvRect = new Rect(0, 1, 1, -1);
    _image.enabled = false;
  }

  public Canvas Canvas { get; }

  /// <summary>Shows these RGBA pixels, rows from the top, at the layout's size.</summary>
  public void Show(byte[] pixels)
  {
    _texture.LoadRawTextureData(pixels);
    _texture.Apply(false);
    _image.enabled = true;
  }

  /// <summary>Shows the last frame again after a time without the camera.</summary>
  public void ShowLast() => _image.enabled = true;

  public void Hide() => _image.enabled = false;

  public void Dispose()
  {
    if (_root != null)
      Object.Destroy(_root);
    if (_texture != null)
      Object.Destroy(_texture);
  }
}
