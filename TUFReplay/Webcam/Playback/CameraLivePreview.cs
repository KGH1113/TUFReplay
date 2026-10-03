using TUFReplay.Composition;
using TUFReplay.Replay.Sessions;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;
using UnityEngine;

namespace TUFReplay.Webcam.Playback;

internal static class CameraLivePreview
{
  private static CameraPreviewBuffer _buffer;
  private static Texture2D _texture;
  private static byte[] _pixels;
  private static GameObject _root;
  private static WebcamReplayOverlay _overlay;
  internal static Texture Texture => _texture;

  internal static void Tick(bool setupVisible)
  {
    CameraPreviewBuffer buffer = FeatureRegistry.WebcamRecording?.Preview;
    bool cameraEnabled = Main.Settings.WebcamEnabled;
    if (!cameraEnabled || buffer == null)
    {
      Shutdown();
      return;
    }
    if (_buffer != buffer)
    {
      Shutdown();
      _buffer = buffer;
      _root = new GameObject("TUFReplayLiveCamera");
      Object.DontDestroyOnLoad(_root);
      _overlay = _root.AddComponent<WebcamReplayOverlay>();
    }
    bool inGame = ADOBase.isScnGame || (scnEditor.instance != null && scnEditor.instance.playMode);
    bool visible = Main.Settings.WebcamLiveVisible && inGame && !ReplaySessionService.HasActiveContext && !setupVisible;
    buffer.SetPreviewRequested(visible || setupVisible);
    if ((visible || setupVisible) && buffer.TryGetFrameSize(out CameraFrameSize size))
    {
      if (_pixels == null || _pixels.Length != size.ByteCount)
        _pixels = new byte[size.ByteCount];
      if (buffer.TryCopy(_pixels, out CameraFrameSize copiedSize))
      {
        // Keep the old texture until a complete replacement is uploaded, even
        // when the camera changes orientation or a helper write overlaps Tick.
        Texture2D previous = _texture;
        Texture2D next = previous;
        bool resized = previous == null || previous.width != copiedSize.Width || previous.height != copiedSize.Height;
        if (resized)
          next = new Texture2D(copiedSize.Width, copiedSize.Height, TextureFormat.RGBA32, false)
          {
            name = "TUFReplayCameraPreview",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
          };
        next.LoadRawTextureData(_pixels);
        next.Apply(false, false);
        if (resized)
        {
          _texture = next;
          _overlay.InitializePreview(next, copiedSize.Aspect);
          if (previous != null)
            Object.Destroy(previous);
        }
      }
    }
    _overlay.ShowFrame = visible && _texture != null;
  }

  internal static void Shutdown()
  {
    _buffer?.SetPreviewRequested(false);
    if (_root != null)
    {
      _root.SetActive(false);
      Object.Destroy(_root);
    }
    if (_texture != null)
      Object.Destroy(_texture);
    _root = null;
    _overlay = null;
    _texture = null;
    _buffer = null;
    _pixels = null;
  }
}
