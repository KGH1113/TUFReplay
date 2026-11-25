using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TUFReplay.Replay.Rendering.Webcam;

/// <summary>
/// Draws the recorded webcam into a hosted render, frame by frame. OrbitRender used to lay it
/// over the finished video, which encoded the whole picture a second time and held the game
/// while it did. Now it captures the camera with the HUD, and a render with only the camera
/// and microphone keeps its first encode.
/// </summary>
internal sealed class RenderWebcam : IDisposable
{
  private readonly RenderWebcamPlan _plan;
  private readonly string _ffmpeg;
  private RenderWebcamCanvas _canvas;
  private WebcamFrameDecoder _decoder;
  private bool _shown;
  private bool _failed;

  private RenderWebcam(RenderWebcamPlan plan, string ffmpeg)
  {
    _plan = plan;
    _ffmpeg = ffmpeg;
  }

  /// <summary>
  /// The webcam a render draws itself, or null when OrbitRender should compose it instead:
  /// the recording has none, FFmpeg is not installed, or the bundle's camera is invalid.
  /// </summary>
  public static RenderWebcam TryCreate(JObject webcam, string path, string ffmpeg)
  {
    if (webcam == null || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg))
      return null;
    try
    {
      return new RenderWebcam(RenderWebcamPlan.Parse(webcam, path), ffmpeg);
    }
    catch (InvalidOperationException exception)
    {
      Main.Instance?.Log("[Render/Webcam] OrbitRender composes the camera: " + exception.Message);
      return null;
    }
  }

  /// <summary>The canvas OrbitRender captures, or null while no camera shows in this video.</summary>
  public Canvas Canvas => _canvas?.Canvas;

  /// <summary>Places the camera in a video of this size. Before OrbitRender collects the canvases.</summary>
  public void Begin(int width, int height)
  {
    RenderWebcamLayout layout = _plan.Layout(width, height);
    if (layout == null)
      return;
    _canvas = new RenderWebcamCanvas(width, height, layout);
    _decoder = new WebcamFrameDecoder(_ffmpeg, _plan.Path, layout.Filter, layout.Width, layout.Height);
  }

  /// <summary>Shows the camera frame for the video time about to be captured.</summary>
  public void BeforeCapture(RenderTimeline timeline, long videoTimeUs)
  {
    if (_decoder == null || _failed || timeline == null)
      return;
    double? sourceUs = _plan.SourceTimeUs(timeline, videoTimeUs);
    if (!sourceUs.HasValue)
    {
      Hide();
      return;
    }
    try
    {
      if (_decoder.Advance(sourceUs.Value))
        _canvas.Show(_decoder.Current);
      else if (_decoder.Current != null && !_shown)
        _canvas.ShowLast();
      _shown = _decoder.Current != null;
    }
    catch (Exception exception)
    {
      // FFmpeg failing to start or decode must not fail the render: the rest of the video goes
      // on without the camera.
      _failed = true;
      Hide();
      Main.Instance?.Log("[Render/Webcam] The camera stops here: " + exception.Message);
    }
  }

  private void Hide()
  {
    if (!_shown)
      return;
    _canvas.Hide();
    _shown = false;
  }

  public void Dispose()
  {
    _decoder?.Dispose();
    _decoder = null;
    _canvas?.Dispose();
    _canvas = null;
  }
}
