using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay;
using TUFReplay.Webcam.Ipc;
using TUFReplay.Webcam.Models;
using static TestFixture;

internal static class WebcamCropSuite
{
  internal static void RunAll()
  {
    CropDefaultsAndNormalizationPreserveValidBounds();
    CropPatchIsAtomicAndDoesNotRestartCapture();
    CropTextureCoordinatesApplyMirrorAfterSelectingSource();
    LegacyPositionPatchClearsDirectCoordinates();
    Console.WriteLine("TUFReplay camera crop tests passed.");
  }

  private static void CropDefaultsAndNormalizationPreserveValidBounds()
  {
    var settings = new TUFReplaySetting();
    WebcamCropRect crop = WebcamCropRect.Get(settings);
    Near(0, crop.X);
    Near(0, crop.Y);
    Near(1, crop.Width);
    Near(1, crop.Height);
    settings.WebcamCropX = 0.9;
    settings.WebcamCropY = -5;
    settings.WebcamCropWidth = 0.5;
    settings.WebcamCropHeight = 0;
    settings.Normalize();
    Near(0.5, settings.WebcamCropX);
    Near(0, settings.WebcamCropY);
    Near(0.05, settings.WebcamCropHeight);
    settings.WebcamCropX = double.PositiveInfinity;
    settings.WebcamCropWidth = double.NaN;
    settings.WebcamOverlayLeft = double.NegativeInfinity;
    settings.WebcamOverlayTop = -8;
    settings.Normalize();
    Near(0, settings.WebcamCropX);
    Near(1, settings.WebcamCropWidth);
    Assert(settings.WebcamOverlayLeft == null, "Nonfinite direct position survived normalization.");
    Assert(settings.WebcamOverlayTop == -8, "Valid offscreen direct position was restricted.");
    string serialized = JsonConvert.SerializeObject(new WebcamStateDto { Crop = WebcamCropRect.Get(settings) });
    JObject json = JObject.Parse(serialized);
    Assert(json["Crop"]["Width"].Value<double>() == 1, "Crop DTO lost its normalized public contract.");
  }

  private static void CropPatchIsAtomicAndDoesNotRestartCapture()
  {
    foreach (
      string invalid in new[]
      {
        "{\"crop\":null}",
        "{\"crop\":{\"x\":0,\"y\":0,\"width\":0.5}}",
        "{\"crop\":{\"x\":0,\"y\":0,\"width\":0.01,\"height\":1}}",
        "{\"crop\":{\"x\":0.6,\"y\":0,\"width\":0.5,\"height\":1}}",
        "{\"crop\":{\"x\":0,\"y\":0.6,\"width\":1,\"height\":0.5}}",
        "{\"crop\":{\"x\":0,\"y\":0,\"width\":1,\"height\":1,\"unknown\":0}}",
      }
    )
      Assert(!WebcamSettingsPatch.TryParse(JObject.Parse(invalid), out _), "Invalid crop patch was accepted.");
    Assert(
      WebcamSettingsPatch.TryParse(
        JObject.Parse("{\"crop\":{\"x\":0.2,\"y\":0.1,\"width\":0.5,\"height\":0.6}}"),
        out WebcamSettingsPatch patch
      ),
      "Valid crop patch was rejected."
    );
    Assert(!patch.ChangesCapture && !patch.RearmsCapture, "Display crop interrupted camera capture.");
    var settings = new TUFReplaySetting { WebcamEnabled = true, WebcamQuality = "quality" };
    patch.ApplyTo(settings);
    Near(0.2, settings.WebcamCropX);
    Near(0.6, settings.WebcamCropHeight);
    Assert(settings.WebcamEnabled && settings.WebcamQuality == "quality", "Crop changed recording configuration.");
  }

  private static void CropTextureCoordinatesApplyMirrorAfterSelectingSource()
  {
    WebcamCropRect crop = WebcamCropRect.Clamp(0.2, 0.1, 0.5, 0.6);
    WebcamTextureCoordinates normal = crop.TextureCoordinates(false);
    WebcamTextureCoordinates mirror = crop.TextureCoordinates(true);
    Near(0.2, normal.X);
    Near(0.3, normal.Y);
    Near(0.5, normal.Width);
    Near(0.7, mirror.X);
    Near(-0.5, mirror.Width);
    Near(normal.Y, mirror.Y);
    WebcamTextureCoordinates vertical = crop.TextureCoordinates(false, true);
    WebcamTextureCoordinates both = crop.TextureCoordinates(true, true);
    Near(0.9, vertical.Y);
    Near(-0.6, vertical.Height);
    Near(mirror.X, both.X);
    Near(mirror.Width, both.Width);
    Near(vertical.Y, both.Y);
    Near(vertical.Height, both.Height);
    Assert(
      WebcamSettingsPatch.TryParse(JObject.Parse("{\"mirror\":true,\"flipVertical\":true}"), out var flips),
      "Flip patch rejected."
    );
    Assert(!flips.ChangesCapture && !flips.RearmsCapture, "Flipping interrupted capture.");
    var settings = new TUFReplaySetting();
    flips.ApplyTo(settings);
    Assert(settings.WebcamMirror && settings.WebcamFlipVertical, "Flip patch was not persisted.");
    Near(16d / 9 * 0.5 / 0.6, crop.AspectRatio(16d / 9));
    Near(0.5 / 0.6, crop.AspectRatio(double.NaN));
  }

  private static void LegacyPositionPatchClearsDirectCoordinates()
  {
    var settings = new TUFReplaySetting { WebcamOverlayLeft = -0.2, WebcamOverlayTop = 0.3 };
    Assert(
      WebcamSettingsPatch.TryParse(JObject.Parse("{\"overlayWidth\":0.3}"), out WebcamSettingsPatch width),
      "Width patch failed."
    );
    width.ApplyTo(settings);
    Assert(
      settings.WebcamOverlayLeft == -0.2 && settings.WebcamOverlayTop == 0.3,
      "Width change lost direct position."
    );
    Assert(
      WebcamSettingsPatch.TryParse(JObject.Parse("{\"overlayX\":1}"), out WebcamSettingsPatch position),
      "Position patch failed."
    );
    position.ApplyTo(settings);
    Assert(
      settings.WebcamOverlayLeft == null && settings.WebcamOverlayTop == null,
      "Legacy position did not reset both direct coordinates."
    );
  }

  private static void Near(double expected, double actual) =>
    Assert(Math.Abs(expected - actual) < 1e-9, "Unexpected crop coordinate or aspect ratio.");
}
