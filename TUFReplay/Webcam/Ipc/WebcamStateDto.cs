using System.Collections.Generic;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Ipc;

public sealed class WebcamStateDto
{
  public bool Supported;
  public string Backend;
  public bool Enabled;
  public bool CaptureLocked;
  public string Status;
  public string Error;
  public List<WebcamDevice> Devices;
  public string SelectedDeviceId;
  public string Quality;
  public int OffsetMs;
  public int StorageLimitMb;
  public int RetentionDays;
  public bool PlaybackVisible;
  public bool LiveVisible;
  public bool Mirror;
  public double OverlayX;
  public double OverlayY;
  public double OverlayWidth;
  public WebcamCropRect Crop;
}
