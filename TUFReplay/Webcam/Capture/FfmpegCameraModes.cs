using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

internal sealed class FfmpegCameraMode
{
  public FfmpegCameraMode(int width, int height, double frameRate, string format, bool compressed)
    : this(width, height, FfmpegCameraFrameRate.Create(frameRate), format, compressed) { }

  public FfmpegCameraMode(int width, int height, FfmpegCameraFrameRate frameRate, string format, bool compressed)
  {
    if (frameRate == null)
      throw new ArgumentNullException(nameof(frameRate));
    Width = width;
    Height = height;
    FrameRate = frameRate.FrameRate;
    FrameRateArgument = frameRate.Argument;
    IntervalTicks = frameRate.IntervalTicks;
    Format = format;
    Compressed = compressed;
  }

  public int Width { get; }
  public int Height { get; }
  public double FrameRate { get; }
  public string FrameRateArgument { get; }
  public long IntervalTicks { get; }
  public string Format { get; }
  public bool Compressed { get; }

  public IEnumerable<string> InputArguments()
  {
    yield return "-video_size";
    yield return Width.ToString(CultureInfo.InvariantCulture) + "x" + Height.ToString(CultureInfo.InvariantCulture);
    yield return "-framerate";
    yield return FrameRateArgument;
    yield return Compressed ? "-vcodec" : "-pixel_format";
    yield return Format;
  }

  public override string ToString() =>
    Width
    + "x"
    + Height
    + " @ "
    + FrameRate.ToString("0.###", CultureInfo.InvariantCulture)
    + " "
    + Format
    + " (fps="
    + FrameRateArgument
    + ", interval="
    + IntervalTicks.ToString(CultureInfo.InvariantCulture)
    + ")";
}

internal static class FfmpegCameraModes
{
  private static readonly Regex Capability = new Regex(
    @"\b(?<type>pixel_format|vcodec)=(?<format>[a-zA-Z0-9_]+)\s+min s=(?<minWidth>\d+)x(?<minHeight>\d+) fps=(?<minRate>[\d.eE+/\-]+)\s+max s=(?<maxWidth>\d+)x(?<maxHeight>\d+) fps=(?<maxRate>[\d.eE+/\-]+)",
    RegexOptions.CultureInvariant
  );

  // Enumeration is performed only while arming. It does not reopen a camera
  // between runs, and neither device names nor command output are shell code.
  public static async Task<IReadOnlyList<FfmpegCameraMode>> DiscoverAsync(
    string executable,
    string deviceId,
    WebcamCaptureProfile profile,
    Action<string> log = null
  )
  {
    using var process = FfmpegCameraProcess.Create(
      executable,
      new[] { "-nostdin", "-list_options", "true", "-f", "dshow", "-i", "video=" + deviceId }
    );
    var lines = new List<string>();
    process.ErrorDataReceived += (_, args) =>
    {
      if (args.Data == null)
        return;
      lock (lines)
        if (lines.Count < 1024)
          lines.Add(args.Data.Length > 2048 ? args.Data.Substring(0, 2048) : args.Data);
    };
    if (!process.Start())
      throw new IOException("Camera settings could not be read. Reconnect the camera and try again.");
    process.BeginErrorReadLine();
    process.BeginOutputReadLine();
    await Task.Run(() =>
    {
      if (!process.WaitForExit(10000))
      {
        try
        {
          process.Kill();
          process.WaitForExit(2000);
        }
        catch (InvalidOperationException) { }
        throw new IOException("The camera did not respond while reading its settings. Reconnect it and try again.");
      }
      process.WaitForExit();
    });
    // FFmpeg intentionally exits with an error after -list_options. An empty
    // listing lets the caller try the device default without forcing 60 fps.
    lock (lines)
    {
      int loggedModes = 0;
      foreach (string line in lines)
      {
        if (line.StartsWith("ffmpeg version ", StringComparison.Ordinal))
          log?.Invoke("Camera FFmpeg: " + line);
        Match capability = Capability.Match(line);
        if (capability.Success && loggedModes++ < 64)
          log?.Invoke("Camera capability: " + capability.Value);
      }
      if (loggedModes > 64)
        log?.Invoke("Camera capability log truncated: " + loggedModes + " entries.");
      return Parse(lines, profile);
    }
  }

  public static IReadOnlyList<FfmpegCameraMode> Parse(IEnumerable<string> output, WebcamCaptureProfile profile)
  {
    if (profile == null)
      throw new ArgumentNullException(nameof(profile));
    var modes = new List<FfmpegCameraMode>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (string line in output)
    {
      if (line == null)
        continue;
      Match match = Capability.Match(line);
      if (
        !match.Success
        || !TryDimension(match.Groups["minWidth"].Value, out int minWidth)
        || !TryDimension(match.Groups["minHeight"].Value, out int minHeight)
        || !TryDimension(match.Groups["maxWidth"].Value, out int maxWidth)
        || !TryDimension(match.Groups["maxHeight"].Value, out int maxHeight)
        || minWidth > maxWidth
        || minHeight > maxHeight
        || !TryFrameRate(match.Groups["minRate"].Value, out double minRate)
        || !TryFrameRate(match.Groups["maxRate"].Value, out double maxRate)
        || minRate > maxRate
      )
        continue;
      double rate = Math.Max(minRate, Math.Min(profile.FrameRate, maxRate));
      if (!FfmpegCameraFrameRate.TryCreate(rate, out FfmpegCameraFrameRate frameRate))
        continue;
      string format = match.Groups["format"].Value;
      bool compressed = match.Groups["type"].Value == "vcodec";
      // The listing omits resolution step sizes. Request advertised endpoints
      // only, rather than inventing a potentially unsupported size in a range.
      Add(minWidth, minHeight);
      Add(maxWidth, maxHeight);

      void Add(int width, int height)
      {
        var mode = new FfmpegCameraMode(width, height, frameRate, format, compressed);
        if (seen.Add(ModeKey(mode)))
          modes.Add(mode);
      }
    }
    return modes
      // A working low-rate native mode must not hide a smoother mode that needs
      // scaling. Try the requested rate first, then 30 fps for a 60 fps profile.
      .OrderBy(mode => FrameRatePriority(mode, profile))
      .ThenByDescending(mode => FrameRatePriority(mode, profile) == 2 ? mode.FrameRate : 0)
      .ThenByDescending(mode => OutputPixels(mode, profile))
      .ThenBy(mode => (long)mode.Width * mode.Height)
      .ThenByDescending(mode => Math.Min(mode.FrameRate, profile.FrameRate))
      .ThenBy(mode => mode.FrameRate)
      .ThenBy(mode =>
        mode.Format == "mjpeg" ? 0
        : mode.Compressed ? 2
        : 1
      )
      .ThenBy(mode => mode.Format, StringComparer.Ordinal)
      .ToArray();
  }

  public static IReadOnlyList<FfmpegCameraMode> BuildAttempts(
    IEnumerable<FfmpegCameraMode> modes,
    WebcamCaptureProfile profile
  )
  {
    if (modes == null)
      throw new ArgumentNullException(nameof(modes));
    if (profile == null)
      throw new ArgumentNullException(nameof(profile));
    var seen = new HashSet<string>(StringComparer.Ordinal);
    FfmpegCameraMode[] uniqueModes = modes.Where(mode => mode != null && seen.Add(ModeKey(mode))).ToArray();
    List<FfmpegCameraMode> attempts = uniqueModes.Where(mode => FrameRatePriority(mode, profile) < 2).Take(8).ToList();
    // Give native driver negotiation a chance before accepting a slow raw mode.
    // The caller also bounds the total startup time across all attempts.
    attempts.Add(null);
    attempts.AddRange(uniqueModes.Where(mode => FrameRatePriority(mode, profile) == 2).Take(3));
    return attempts;
  }

  private static string ModeKey(FfmpegCameraMode mode) =>
    mode.Width + ":" + mode.Height + ":" + mode.FrameRateArgument + ":" + mode.Compressed + ":" + mode.Format;

  private static int FrameRatePriority(FfmpegCameraMode mode, WebcamCaptureProfile profile)
  {
    // Treat common fractional rates (29.97/59.94) as their nominal targets.
    if (mode.FrameRate >= profile.FrameRate * 0.995)
      return 0;
    if (mode.FrameRate >= Math.Min(30, profile.FrameRate) * 0.995)
      return 1;
    return 2;
  }

  private static long OutputPixels(FfmpegCameraMode mode, WebcamCaptureProfile profile)
  {
    CameraFrameSize size = CameraFrameSize.Fit(mode.Width, mode.Height, profile.Width, profile.Height);
    return (long)size.Width * size.Height;
  }

  private static bool TryDimension(string value, out int dimension) =>
    int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out dimension)
    && dimension >= 2
    && dimension <= 16384;

  private static bool TryFrameRate(string value, out double rate)
  {
    string[] parts = value.Split('/');
    rate = 0;
    if (
      parts.Length > 2
      || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator)
    )
      return false;
    double denominator = 1;
    if (
      parts.Length == 2
      && !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out denominator)
    )
      return false;
    rate = numerator / denominator;
    return rate > 0 && rate <= 1000 && !double.IsInfinity(rate) && !double.IsNaN(rate);
  }
}
