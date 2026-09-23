using System.IO;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Visual.Importing.Assets;

/// <summary>Portable source sprites, stored byte-for-byte with their original license.</summary>
internal static class VisualBuiltInAssets
{
  public static string AddGameCjk(VisualAssetCollector assets)
  {
    const string path = "assets/builtin/adofai-cjk.woff";
    if (assets.Contains(path))
      return path;
    using (
      Stream stream = typeof(VisualBuiltInAssets).Assembly.GetManifestResourceStream(
        "TUFReplay.Visual.Assets.Fonts.adofai-cjk.woff"
      )
    )
    {
      if (stream == null)
        throw new VisualImportException("visual_bundle_invalid", "The built-in game CJK font is unavailable.");
      using (var memory = new MemoryStream())
      {
        stream.CopyTo(memory);
        assets.AddEmbedded(path, "font/woff", System.Convert.ToBase64String(memory.ToArray()));
      }
    }
    return path;
  }

  public static string AddPretendard(VisualAssetCollector assets) => AddFont(assets, "PretendardVariable.woff2");

  public static string AddFont(VisualAssetCollector assets, string name)
  {
    string path = "assets/builtin/" + name;
    if (assets.Contains(path))
      return path;
    using (
      Stream stream = typeof(VisualBuiltInAssets).Assembly.GetManifestResourceStream(
        "TUFReplay.Visual.Assets.Fonts." + name
      )
    )
    {
      if (stream == null)
        throw new VisualImportException("visual_bundle_invalid", "The built-in DMNote font is unavailable.");
      string media =
        name.EndsWith(".woff2") ? "font/woff2"
        : name.EndsWith(".woff") ? "font/woff"
        : name.EndsWith(".otf") ? "font/otf"
        : "font/ttf";
      using (var reader = new StreamReader(stream))
        assets.AddEmbedded(path, media, reader.ReadToEnd().Trim());
    }
    return path;
  }

  public static string DmnoteFontFile(string family, bool legacyPretendard) =>
    family.Trim().Trim('\'', '"') switch
    {
      "Pretendard Variable" => "PretendardVariable.woff2",
      "Pretendard" => legacyPretendard ? "Pretendard-Regular.woff" : "PretendardVariable.woff2",
      "SUIT" or "SUIT-Regular" => "SUIT-Regular.woff2",
      "IsYun" => "LeeSeoyun.woff",
      "RoundedFixedsys" => "DungGeunMo.woff",
      _ => null,
    };

  public static string DmnoteFontUrl(string url) =>
    url switch
    {
      "https://fastly.jsdelivr.net/gh/projectnoonnu/noonfonts_suit@1.0/SUIT-Regular.woff2" => "SUIT-Regular.woff2",
      "https://fastly.jsdelivr.net/gh/Project-Noonnu/noonfonts_2107@1.1/Pretendard-Regular.woff" =>
        "Pretendard-Regular.woff",
      "https://cdn.jsdelivr.net/gh/projectnoonnu/noonfonts_2202-2@1.0/LeeSeoyun.woff" => "LeeSeoyun.woff",
      "https://cdn.jsdelivr.net/gh/projectnoonnu/noonfonts_six@1.2/DungGeunMo.woff" => "DungGeunMo.woff",
      _ => null,
    };

  public static JObject AddJipperSprites(VisualAssetCollector assets, JObject references = null)
  {
    references = references ?? new JObject();
    foreach (string name in new[] { "KeyBackground", "KeyOutline", "GhostRain" })
    {
      if (references[name + "Image"] != null)
        continue;
      string path = "assets/builtin/jipper/" + name + ".png";
      using (
        Stream stream = typeof(VisualBuiltInAssets).Assembly.GetManifestResourceStream(
          "TUFReplay.Visual.Assets.Jipper." + name + ".png"
        )
      )
      {
        if (stream == null)
          throw new VisualImportException("visual_bundle_invalid", "The built-in keyviewer images are unavailable.");
        using (var reader = new StreamReader(stream))
          assets.AddEmbedded(path, "image/png", reader.ReadToEnd().Trim());
      }
      references[name + "Image"] = path;
    }
    return references;
  }

  public static JObject AddJipperProgressSprite(VisualAssetCollector assets)
  {
    const string path = "assets/builtin/jipper/ProgressBackground.png";
    using (
      Stream stream = typeof(VisualBuiltInAssets).Assembly.GetManifestResourceStream(
        "TUFReplay.Visual.Assets.Jipper.ProgressBackground.png"
      )
    )
    using (var reader = new StreamReader(stream))
      assets.AddEmbedded(path, "image/png", reader.ReadToEnd().Trim());
    return new JObject { ["ProgressBackgroundImage"] = path };
  }
}
