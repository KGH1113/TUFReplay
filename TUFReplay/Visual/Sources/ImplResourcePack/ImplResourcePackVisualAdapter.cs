using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Importing.Assets;
using TUFReplay.Visual.Importing.Building;
using TUFReplay.Visual.Importing.Security;

namespace TUFReplay.Visual.Sources.ImplResourcePack;

/// <summary>Snapshots ImplResourcePack's fixed 1920x1080 overlay and saved visual options.</summary>
public sealed class ImplResourcePackVisualAdapter : IVisualSourceAdapter
{
  private readonly IVisualFileSystem _files;
  private readonly IVisualSourceLocator _locator;
  public VisualSource Source => VisualSource.ImplResourcePack;

  public ImplResourcePackVisualAdapter(IVisualFileSystem files, IVisualSourceLocator locator)
  {
    _files = files ?? throw new ArgumentNullException(nameof(files));
    _locator = locator ?? throw new ArgumentNullException(nameof(locator));
  }

  public VisualBundle Import(VisualKind kind, string presetJson = null, VisualImportOptions options = null)
  {
    if (kind != VisualKind.Overlay || presetJson != null)
      throw new VisualImportException(
        "visual_source_unsupported",
        "ImplResourcePack supports automatic overlay import only."
      );
    var installation = _locator.Find(Source);
    if (installation == null)
      throw new VisualImportException(
        "visual_source_unsupported",
        "Install ImplResourcePack before importing its overlay."
      );
    var settings = ReadSettings(installation.RootPath);
    // Layout is implemented in source, not in a user JSON file. Version the
    // layout contract explicitly so a consumer can select the matching renderer.
    var snapshot = new JObject
    {
      ["layoutVersion"] = 1,
      ["Setting"] = new JObject
      {
        ["FontName"] = "MAPLESTORY_OTF_BOLD",
        ["width"] = 1920,
        ["height"] = 1080,
      },
      ["HidePerfectJudgmentText"] = settings.Item1,
      ["RecordMode"] = settings.Item2,
      ["Theme"] = new JObject
      {
        ["accent"] = "#BA712B",
        ["judgementBase"] = "#D958FF",
        ["bpmColorMaximum"] = 8000,
      },
      ["Panels"] = new JObject
      {
        ["Status"] = Panel(16, 16, 456, 135, 25, "top-left"),
        ["BPM"] = Panel(16, 16, 456, 90, 25, "top-right"),
        ["Combo"] = Panel(0, 57, 300, 200, 108, "top-center"),
        ["Judgement"] = Panel(0, 85, 1000, 30, 25, "bottom-center"),
      },
    };
    // Both source fonts are portable embedded assets. The game supplies this
    // fallback automatically, so registration must never ask the user for it.
    var assets = new VisualAssetCollector(_files, installation.RootPath, installation.RootPath, options: options);
    assets.Collect(snapshot);
    snapshot["Setting"]["FallbackFontName"] = VisualBuiltInAssets.AddGameCjk(assets);
    return VisualBundleBuilder.CreateBundle(
      kind,
      Source,
      installation.Version,
      VisualBundleBuilder.ViewportFrom(snapshot),
      new Dictionary<string, JToken> { ["ImplResourcePack.json"] = snapshot },
      assets
    );
  }

  private static JObject Panel(int x, int y, int width, int height, int fontSize, string anchor) =>
    new JObject
    {
      ["x"] = x,
      ["y"] = y,
      ["width"] = width,
      ["height"] = height,
      ["fontSize"] = fontSize,
      ["anchor"] = anchor,
    };

  private Tuple<bool, bool> ReadSettings(string root)
  {
    string path = Path.Combine(root, "Settings.xml");
    if (!_files.FileExists(path))
      return Tuple.Create(true, false);
    if (!VisualPathPolicy.IsContained(root, path))
      throw new VisualImportException(
        "visual_bundle_invalid",
        "The overlay settings must be inside the discovered mod directory."
      );
    if (_files.FileLength(path) > VisualImportLimits.MaxFileBytes)
      throw new VisualImportException("visual_payload_too_large", "The overlay settings file is too large.");
    try
    {
      using (
        var reader = XmlReader.Create(
          new StringReader(_files.ReadAllText(path)),
          new XmlReaderSettings
          {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = VisualImportLimits.MaxFileBytes,
          }
        )
      )
      {
        XElement value = XDocument.Load(reader).Root;
        if (value == null)
          throw new XmlException("Missing settings root.");
        return Tuple.Create(
          ReadBoolean(value, "HidePerfectJudgmentText", true),
          ReadBoolean(value, "RecordMode", false)
        );
      }
    }
    catch (Exception exception) when (exception is XmlException || exception is FormatException)
    {
      throw new VisualImportException(
        "visual_bundle_invalid",
        "Save ImplResourcePack settings again before importing.",
        exception
      );
    }
  }

  private static bool ReadBoolean(XElement root, string name, bool fallback) =>
    root.Element(name) == null ? fallback : XmlConvert.ToBoolean(root.Element(name).Value.Trim());
}
