using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Replay.Rendering.Webcam;

/// <summary>
/// When and where the recorded webcam shows in a render: which camera time each video time
/// shows, and the video pixels it covers. Both follow OrbitRender's webcam composition, which
/// drew the camera over the finished video before TUFReplay drew it during the render.
/// </summary>
internal sealed class RenderWebcamPlan
{
  private readonly Segment[] _segments;
  private readonly double _durationUs;
  private readonly double _captureStartOffsetUs;
  private readonly double _cameraRate;
  private readonly double _offsetUs;
  private readonly double _cropLeft,
    _cropTop,
    _cropRight,
    _cropBottom;
  private readonly double _left,
    _top,
    _width,
    _height;
  private readonly bool _mirror;
  private readonly bool _flipVertical;

  /// <summary>The camera recording's absolute path.</summary>
  public string Path { get; }

  private RenderWebcamPlan(JObject webcam, string path)
  {
    Path = path;
    _segments = ((webcam["timeline"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
      .Select(segment => new Segment(
        RequiredNumber(segment, "timelineTimeUs"),
        RequiredNumber(segment, "videoTimeUs"),
        RequiredPositive(segment, "gameplayRate")
      ))
      .ToArray();
    _durationUs = RequiredPositive(webcam, "durationUs");
    _captureStartOffsetUs = RequiredNumber(webcam, "captureStartOffsetUs");
    _cameraRate = RequiredPositive(webcam, "gameplayRate");
    double offsetMs = (double?)webcam["offsetMs"] ?? 0;
    if (double.IsNaN(offsetMs) || double.IsInfinity(offsetMs))
      throw new InvalidOperationException("Invalid media offsetMs");
    _offsetUs = offsetMs * 1000;
    JObject crop = webcam["crop"] as JObject ?? new JObject();
    _cropLeft = Unit(crop, "left", 0);
    _cropTop = Unit(crop, "top", 0);
    _cropRight = Unit(crop, "right", 1);
    _cropBottom = Unit(crop, "bottom", 1);
    if (_cropRight <= _cropLeft || _cropBottom <= _cropTop)
      throw new InvalidOperationException("Camera crop removes the whole image.");
    JObject layout = webcam["layout"] as JObject ?? new JObject();
    _left = Position(layout, "left", .72);
    _top = Position(layout, "top", .72);
    _width = Extent(layout, "width", .25);
    _height = Extent(layout, "height", .25);
    _mirror = (bool?)webcam["mirror"] ?? false;
    _flipVertical = (bool?)webcam["flipVertical"] ?? false;
  }

  /// <summary>
  /// The bundle's <c>media.webcam</c> with its recording at <paramref name="path"/>. Throws
  /// <see cref="InvalidOperationException"/> on a value OrbitRender would also refuse.
  /// </summary>
  public static RenderWebcamPlan Parse(JObject webcam, string path) =>
    new RenderWebcamPlan(webcam ?? throw new ArgumentNullException(nameof(webcam)), path);

  /// <summary>
  /// The camera time, in microseconds from the recording's first frame, that the video shows at
  /// <paramref name="outputUs"/>, or null while it shows no camera: before the recording starts
  /// or after it ends.
  /// </summary>
  public double? SourceTimeUs(RenderTimeline timeline, double outputUs)
  {
    double replay = timeline.OutputToReplay(outputUs);
    Segment segment = null;
    foreach (Segment candidate in _segments)
      if (candidate.TimelineTimeUs <= replay)
        segment = candidate;
    double source;
    if (segment != null)
      source = segment.VideoTimeUs + (replay - segment.TimelineTimeUs) / segment.Rate + _offsetUs;
    else
    {
      long? clear = timeline.GameplayEndTimeUs;
      bool afterClear = clear.HasValue && replay >= clear.Value;
      // After the clear or death the camera ran at real time, like the replay.
      source =
        (afterClear ? clear.Value / _cameraRate + replay - clear.Value : replay / _cameraRate)
        - _captureStartOffsetUs
        + _offsetUs;
    }
    return source >= 0 && source < _durationUs ? source : null;
  }

  /// <summary>
  /// The camera's place in a <paramref name="width"/> x <paramref name="height"/> video, and the
  /// FFmpeg filter that turns a recorded frame into exactly those pixels; null when it is
  /// entirely off the video. A camera partly off the video keeps only its visible part.
  /// </summary>
  public RenderWebcamLayout Layout(int width, int height)
  {
    if (width <= 0 || height <= 0)
      throw new ArgumentOutOfRangeException(nameof(width));
    double fullWidth = _width * width,
      fullHeight = _height * height;
    double originalX = _left * width,
      originalY = _top * height;
    double visibleLeft = Math.Max(0, originalX),
      visibleTop = Math.Max(0, originalY);
    double visibleRight = Math.Min(width, originalX + fullWidth),
      visibleBottom = Math.Min(height, originalY + fullHeight);
    if (visibleRight - visibleLeft < 2 || visibleBottom - visibleTop < 2)
      return null;
    int x = (int)Math.Round(visibleLeft),
      y = (int)Math.Round(visibleTop);
    // Even sizes, as OrbitRender drew them over chroma-subsampled video.
    int w = Math.Max(2, (int)Math.Round(visibleRight - visibleLeft) / 2 * 2);
    int h = Math.Max(2, (int)Math.Round(visibleBottom - visibleTop) / 2 * 2);
    // Clip the source before scaling, so a narrow crop never makes an oversized frame.
    string viewport =
      "crop=max(2\\,iw*"
      + N((visibleRight - visibleLeft) / fullWidth)
      + "):max(2\\,ih*"
      + N((visibleBottom - visibleTop) / fullHeight)
      + "):iw*"
      + N((visibleLeft - originalX) / fullWidth)
      + ":ih*"
      + N((visibleTop - originalY) / fullHeight);
    string filter =
      "crop=iw*"
      + N(_cropRight - _cropLeft)
      + ":ih*"
      + N(_cropBottom - _cropTop)
      + ":iw*"
      + N(_cropLeft)
      + ":ih*"
      + N(_cropTop)
      + (_mirror ? ",hflip" : "")
      + (_flipVertical ? ",vflip" : "")
      + ","
      + viewport
      + ",scale="
      + w
      + ":"
      + h
      + ":flags=lanczos";
    return new RenderWebcamLayout(x, y, w, h, filter);
  }

  private static string N(double value) => value.ToString("0.#########", CultureInfo.InvariantCulture);

  private static double Unit(JObject obj, string key, double fallback)
  {
    double value = (double?)obj[key] ?? fallback;
    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
      throw new InvalidOperationException("Invalid layout " + key);
    return value;
  }

  private static double Position(JObject obj, string key, double fallback)
  {
    double value = (double?)obj[key] ?? fallback;
    if (double.IsNaN(value) || double.IsInfinity(value))
      throw new InvalidOperationException("Invalid overlay position " + key);
    return value;
  }

  private static double Extent(JObject obj, string key, double fallback)
  {
    double value = (double?)obj[key] ?? fallback;
    if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 64)
      throw new InvalidOperationException("Invalid overlay size " + key);
    return value;
  }

  private static double RequiredNumber(JObject obj, string key)
  {
    double? value = (double?)obj[key];
    if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
      throw new InvalidOperationException("Invalid media " + key);
    return value.Value;
  }

  private static double RequiredPositive(JObject obj, string key)
  {
    double value = RequiredNumber(obj, key);
    if (value <= 0)
      throw new InvalidOperationException("Invalid media " + key);
    return value;
  }

  private sealed class Segment
  {
    public Segment(double timelineTimeUs, double videoTimeUs, double rate)
    {
      TimelineTimeUs = timelineTimeUs;
      VideoTimeUs = videoTimeUs;
      Rate = rate;
    }

    public double TimelineTimeUs { get; }
    public double VideoTimeUs { get; }
    public double Rate { get; }
  }
}

/// <summary>The camera's pixels in the video, and the FFmpeg filter that makes them.</summary>
internal sealed class RenderWebcamLayout
{
  public RenderWebcamLayout(int x, int y, int width, int height, string filter)
  {
    X = x;
    Y = y;
    Width = width;
    Height = height;
    Filter = filter;
  }

  /// <summary>Video pixels from the top left.</summary>
  public int X { get; }
  public int Y { get; }
  public int Width { get; }
  public int Height { get; }
  public string Filter { get; }
}
