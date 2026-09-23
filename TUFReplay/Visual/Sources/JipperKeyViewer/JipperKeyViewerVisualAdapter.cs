using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Importing.Assets;
using TUFReplay.Visual.Importing.Building;
using TUFReplay.Visual.Importing.Json;
using TUFReplay.Visual.Importing.Security;

namespace TUFReplay.Visual.Sources.JipperKeyViewer;

/// <summary>The standalone JipperKeyViewer mod, independent of JipperResourcePack.</summary>
public sealed class JipperKeyViewerVisualAdapter : IVisualSourceAdapter
{
  private static readonly HashSet<string> SystemFontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  {
    "Arial",
    "Helvetica",
    "Verdana",
    "Tahoma",
    "Georgia",
    "Times New Roman",
    "Courier New",
    "Trebuchet MS",
    "Segoe UI",
    "Liberation Sans",
  };
  private readonly IVisualFileSystem _files;
  private readonly IVisualSourceLocator _locator;
  private readonly Func<int, string> _runtimeFontName;
  public VisualSource Source => VisualSource.JipperKeyviewer;

  public JipperKeyViewerVisualAdapter(
    IVisualFileSystem files,
    IVisualSourceLocator locator,
    Func<int, string> runtimeFontName = null
  )
  {
    _files = files ?? throw new ArgumentNullException(nameof(files));
    _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    _runtimeFontName = runtimeFontName ?? JipperKeyViewerRuntimeFontResolver.Resolve;
  }

  public VisualBundle Import(VisualKind kind, string presetJson = null, VisualImportOptions options = null)
  {
    if (kind != VisualKind.Keyviewer || presetJson != null)
      throw new VisualImportException(
        "visual_source_unsupported",
        "Jipper KeyViewer reads its saved keyviewer profile automatically."
      );
    options = options ?? new VisualImportOptions();
    var installation = _locator.Find(Source);
    if (installation == null)
      throw new VisualImportException(
        "visual_source_unsupported",
        "Install Jipper KeyViewer and save its settings before importing."
      );
    string root = installation.RootPath;
    JObject meta = Read(root, Path.Combine(root, "config", "settings.json"));
    // Earlier config versions need the source mod's coordinate/layout migrations.
    // Never reinterpret their saved positions as the current format.
    if (meta["Version"]?.Type != JTokenType.Integer || meta["Version"].Value<int>() != 6)
      throw new VisualImportException(
        "visual_source_unsupported",
        "Open Jipper KeyViewer 1.7.2 and save the current profile before importing."
      );
    string name = meta["CurrentProfile"]?.Type == JTokenType.String ? meta["CurrentProfile"].Value<string>() : null;
    if (string.IsNullOrWhiteSpace(name))
      name = "Default";
    foreach (char c in Path.GetInvalidFileNameChars())
      name = name.Replace(c, '_');
    JObject profile = Read(root, Path.Combine(root, "config", "profiles", name + ".json"));
    var selected = (JObject)VisualJsonReader.Sanitize(profile);
    selected["FallbackFontName"] = "assets/cjkFonts-regular-normalized.otf";
    string font = selected["FontName"]?.Type == JTokenType.String ? selected["FontName"].Value<string>() : null;
    if (string.IsNullOrWhiteSpace(font))
    {
      int index = selected["FontIndex"]?.Type == JTokenType.Integer ? selected["FontIndex"].Value<int>() : 1;
      font = _runtimeFontName(index);
      if (string.IsNullOrWhiteSpace(font))
        font = "Jipper KeyViewer font index " + index;
      selected["FontName"] = font;
    }
    if (
      font == "CJK (Default)"
      || string.Equals(font, "cjkFonts-regular-normalized", StringComparison.OrdinalIgnoreCase)
      || string.Equals(font, "cjkFonts-regular-normalized SDF", StringComparison.OrdinalIgnoreCase)
    )
      selected["FontName"] = "assets/cjkFonts-regular-normalized.otf";
    else if (font == "MapleStory")
      selected["FontName"] = "assets/MAPLESTORY_OTF_BOLD.OTF";
    else if (font.StartsWith("Custom: ", StringComparison.Ordinal))
    {
      string fileName = font.Substring("Custom: ".Length);
      // Match the mod's TTF-before-OTF lookup without scanning arbitrary paths.
      if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !fileName.Contains("\\"))
        foreach (string extension in new[] { ".ttf", ".otf" })
        {
          string relative = "CustomFont/" + fileName + extension;
          string path = Path.Combine(root, relative);
          if (!VisualPathPolicy.IsContained(root, path) || !_files.FileExists(path))
            continue;
          selected["FontName"] = relative;
          break;
        }
    }
    // Persist native field names, arrays, FreeMake nodes and layer groups.
    var snapshot = new JObject { ["Version"] = 6, ["Data"] = selected };
    var resources = new JObject();
    foreach (string sprite in new[] { "KeyBackground", "KeyOutline", "GhostRain" })
    {
      string relative = "assets/" + sprite + ".png";
      if (_files.FileExists(Path.Combine(root, relative)))
        resources[sprite + "Image"] = relative;
    }
    snapshot["Resources"] = resources;
    NormalizeImages(selected, root, options);
    var assets = new VisualAssetCollector(_files, root, root, options: options, portableFontFamilies: SystemFontNames);
    assets.Collect(snapshot);
    snapshot["Resources"] = VisualBuiltInAssets.AddJipperSprites(assets, resources);
    return VisualBundleBuilder.CreateBundle(
      kind,
      Source,
      installation.Version,
      VisualBundleBuilder.ViewportFrom(null),
      new Dictionary<string, JToken> { ["JipperKeyViewer.json"] = snapshot },
      assets
    );
  }

  private void NormalizeImages(JToken value, string root, VisualImportOptions options)
  {
    if (value is JArray array)
    {
      foreach (JToken child in array)
        NormalizeImages(child, root, options);
      return;
    }
    if (!(value is JObject obj))
      return;
    foreach (JProperty property in obj.Properties())
    {
      if (
        (property.Name != "ImagePath" && property.Name != "ImagePathPressed")
        || property.Value.Type != JTokenType.String
      )
      {
        NormalizeImages(property.Value, root, options);
        continue;
      }
      string reference = property.Value.Value<string>();
      if (string.IsNullOrWhiteSpace(reference) || options.Find(reference) != null)
        continue;
      // The source resolves relative images under CustomImages, absolute images
      // anywhere. Only auto-read contained files; external files require attachment.
      string candidate = Path.IsPathRooted(reference) ? reference : Path.Combine(root, "CustomImages", reference);
      if (VisualPathPolicy.IsContained(root, candidate) && _files.FileExists(candidate))
      {
        property.Value = Path.GetRelativePath(root, candidate).Replace('\\', '/');
        continue;
      }
      options.Missing(reference, "image");
      property.Value = ""; // inspection can return the original reference without reading it
    }
  }

  private JObject Read(string root, string path)
  {
    if (!VisualPathPolicy.IsContained(root, path))
      throw new VisualImportException(
        "visual_bundle_invalid",
        "The saved profile must be inside the discovered mod directory."
      );
    return VisualJsonReader.ReadObject(_files, path);
  }
}
