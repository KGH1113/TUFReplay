using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Infrastructure.FileSystem;
using TUFReplay.Visual.Sources.DmNote;

internal static class VisualPortabilitySuite
{
  public static void Run()
  {
    foreach (var source in new[] { VisualSource.Dmnote, VisualSource.ImplDmnote })
    {
      var adapter = new DmNoteVisualAdapter(new PhysicalVisualFileSystem(), source);
      var preset = JObject.Parse(
        "{\"keys\":{\"one\":[\"A\"],\"two\":[\"B\"]},\"selectedKeyType\":\"one\",\"spritePositions\":{\"one\":[{\"baseImage\":\"https://example.com/base.png\",\"poses\":[{\"imageOverride\":\"blob:pose\"}]}],\"two\":[{\"baseImage\":\"missing-other-tab.png\"}]},\"customCSS\":{\"content\":\".key { background-image: url(https://example.com/bg.avif?v=1); }\"},\"customFonts\":[{\"enabled\":true,\"type\":\"web\",\"name\":\"ExampleFont\",\"cssContent\":\"@font-face {font-family: ExampleFont;src:url(https://example.com/font.woff2?v=1)}\"}],\"sounds\":[\"missing.wav\"],\"embeddedLocalSounds\":[{\"dataBase64\":\"invalid\"}]}"
      );
      var inspection = new VisualImportOptions { Inspect = true };
      adapter.Import(VisualKind.Keyviewer, preset.ToString(), inspection);
      TestFixture.Assert(
        inspection.MissingAssets.Count == 4,
        "Every nonportable sprite/CSS/font reference must have an attachment requirement."
      );
      TestFixture.Assert(
        inspection.MissingAssets.All(a => !a.Reference.Contains("other-tab") && !a.Reference.Contains("wav")),
        "Other tabs and visual-only sound exclusions must not request assets."
      );
      var uploads = inspection
        .MissingAssets.Select(a => new VisualAssetUpload
        {
          Reference = a.Reference,
          DataBase64 = a.Reference.Contains("woff2") ? "d09GMgAAAAA=" : "iVBORw0KGgo=",
        })
        .ToArray();
      var bundle = adapter.Import(
        VisualKind.Keyviewer,
        preset.ToString(),
        new VisualImportOptions { Uploads = uploads }
      );
      var json = (JObject)bundle.Files["preset.json"];
      TestFixture.Assert(
        ((JObject)json["spritePositions"]).Count == 1 && json["sounds"] == null && json["embeddedLocalSounds"] == null,
        "Sprite projection and visual-only sanitization failed."
      );
      TestFixture.Assert(
        !json.ToString().Contains("https://") && !json.ToString().Contains("blob:"),
        "Uploaded resources must replace external references in the portable snapshot."
      );
      TestFixture.Assert(
        bundle.Assets.Any(a =>
          a.Path.EndsWith(source == VisualSource.Dmnote ? "PretendardVariable.woff2" : "SUIT-Regular.woff2")
        ),
        "The source's exact default font must be bundled."
      );
      foreach (
        var format in new[]
        {
          new
          {
            Extension = "ico",
            Mime = "image/x-icon",
            Data = "AAABAAEAAAA=",
          },
          new
          {
            Extension = "avif",
            Mime = "image/avif",
            Data = "AAAAEGZ0eXBhdmlmAAAAAA==",
          },
        }
      )
      {
        var imagePreset = new JObject
        {
          ["keys"] = new JObject { ["one"] = new JArray("A") },
          ["keyPositions"] = new JObject
          {
            ["one"] = new JArray(new JObject { ["activeImage"] = "dmnote-local-image://asset" }),
          },
          ["embeddedLocalImages"] = new JArray(
            new JObject
            {
              ["imageId"] = "asset",
              ["extension"] = format.Extension,
              ["mimeType"] = format.Mime,
              ["dataBase64"] = format.Data,
            }
          ),
        };
        var imageBundle = adapter.Import(VisualKind.Keyviewer, imagePreset.ToString());
        TestFixture.Assert(
          imageBundle.Assets.Any(a => a.MediaType == format.Mime),
          "ICO/AVIF must retain their actual media type."
        );
      }
    }
    Console.WriteLine("Visual portability tests passed.");
  }
}
