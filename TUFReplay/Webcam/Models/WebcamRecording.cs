using System;
using Newtonsoft.Json;

namespace TUFReplay.Webcam.Models;

public sealed class WebcamRecording
{
  public int SchemaVersion { get; set; } = 1;
  public string RunId { get; set; }
  public string DeviceId { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
  public int FrameRate { get; set; }
  public long DurationUs { get; set; }
  public long CaptureStartOffsetUs { get; set; }
  public double GameplayRate { get; set; } = 1d;
  public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
  public bool SizeLimited { get; set; }
  public WebcamTimelineSegment[] Timeline { get; set; } = Array.Empty<WebcamTimelineSegment>();

  [JsonIgnore]
  public string FilePath { get; set; }

  [JsonIgnore]
  public long CaptureStartTimestampTicks { get; set; }
}

public sealed class WebcamTimelineSegment
{
  public long TimelineTimeUs { get; set; }
  public long VideoTimeUs { get; set; }
  public double GameplayRate { get; set; }
}

public sealed class WebcamDevice
{
  public string Id { get; set; }
  public string Name { get; set; }
}
