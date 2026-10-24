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
  {
    Width = width;
    Height = height;
    // DirectShow uses integer 100 ns frame intervals. Its %g listing rounds
    // decimal rates, so reconstruct that interval before requesting the mode.
    // In particular, requesting 60 from a 59.94-only webcam is not supported.
    long interval = Math.Max(1, (long)Math.Round(10_000_000d / frameRate));
    FrameRate = 10_000_000d / interval;
    FrameRateArgument = "10000000/" + interval.ToString(CultureInfo.InvariantCulture);
    Format = format;
    Compressed = compressed;
  }

  public int Width { get; }
  public int Height { get; }
  public double FrameRate { get; }
  public string FrameRateArgument { get; }
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
    Width + "x" + Height + " @ " + FrameRate.ToString("0.###", CultureInfo.InvariantCulture) + " " + Format;
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
    WebcamCaptureProfile profile
  )
  {
    using var process = FfmpegCameraProcess.Create(
      executable,
      new[] { "-hide_banner", "-nostdin", "-list_options", "true", "-f", "dshow", "-i", "video=" + deviceId }
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
      return Parse(lines, profile);
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
      string format = match.Groups["format"].Value;
      bool compressed = match.Groups["type"].Value == "vcodec";
      // The listing omits resolution step sizes. Request advertised endpoints
      // only, rather than inventing a potentially unsupported size in a range.
      Add(minWidth, minHeight);
      Add(maxWidth, maxHeight);

      void Add(int width, int height)
      {
        var mode = new FfmpegCameraMode(width, height, rate, format, compressed);
        string key = width + ":" + height + ":" + mode.FrameRateArgument + ":" + compressed + ":" + format;
        if (seen.Add(key))
          modes.Add(mode);
      }
    }
    return modes
      .OrderByDescending(mode => OutputPixels(mode, profile))
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
