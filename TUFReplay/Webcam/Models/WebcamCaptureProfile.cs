namespace TUFReplay.Webcam.Models;

public sealed class WebcamCaptureProfile
{
  private WebcamCaptureProfile(int width, int height, int frameRate, int bitRate)
  {
    Width = width;
    Height = height;
    FrameRate = frameRate;
    BitRate = bitRate;
  }

  public int Width { get; }
  public int Height { get; }
  public int FrameRate { get; }
  public int BitRate { get; }

  public WebcamCaptureProfile FitToSource(int width, int height)
  {
    CameraFrameSize size = CameraFrameSize.Fit(width, height, Width, Height);
    return new WebcamCaptureProfile(size.Width, size.Height, FrameRate, BitRate);
  }

  public static WebcamCaptureProfile ForQuality(string quality) =>
    quality switch
    {
      "quality" => new WebcamCaptureProfile(1920, 1080, 30, 3_000_000),
      "balanced" => new WebcamCaptureProfile(1280, 720, 30, 1_200_000),
      _ => new WebcamCaptureProfile(640, 480, 30, 600_000),
    };
}
