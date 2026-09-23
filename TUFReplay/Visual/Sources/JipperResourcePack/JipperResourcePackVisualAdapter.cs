using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Importing.Assets;
using TUFReplay.Visual.Importing.Building;
using TUFReplay.Visual.Importing.Json;
using TUFReplay.Visual.Importing.Security;

namespace TUFReplay.Visual.Sources.JipperResourcePack;

public sealed class JipperResourcePackVisualAdapter : IVisualSourceAdapter
{
  private readonly IVisualFileSystem _fileSystem;
  private readonly IVisualSourceLocator _locator;

  public VisualSource Source => VisualSource.JipperResourcePack;

  public JipperResourcePackVisualAdapter(IVisualFileSystem fileSystem, IVisualSourceLocator locator)
  {
    _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    _locator = locator ?? throw new ArgumentNullException(nameof(locator));
  }

  public VisualBundle Import(VisualKind kind, string presetJson = null, VisualImportOptions options = null)
  {
    if (presetJson != null)
      throw new VisualImportException(
        "visual_source_unsupported",
        "JipperResourcePack settings are read automatically from the installed mod."
      );
    VisualSourceInstallation installation = _locator.Find(Source);
    if (installation == null)
      throw new VisualImportException(
        "visual_source_unsupported",
        "A supported JipperResourcePack installation was not found."
      );
    string settingsPath = Path.Combine(installation.RootPath, "Settings.json");
    if (!VisualPathPolicy.IsContained(installation.RootPath, settingsPath))
      throw new VisualImportException(
        "visual_source_unsupported",
        "The JipperResourcePack source configuration is outside its discovered root."
      );
    JObject source = VisualJsonReader.ReadObject(_fileSystem, settingsPath);
    JToken selected = SelectConfiguration(source, kind);
    JToken sanitized = VisualJsonReader.Sanitize(selected);
    if (kind == VisualKind.Keyviewer)
    {
      // JRP stores 0x1000 + a platform-specific native code for extra keys.
      // Preserve the source platform so later playback cannot reinterpret it.
      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        sanitized["nativeKeyPlatform"] = "macos";
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        sanitized["nativeKeyPlatform"] = "windows";
    }
    // JipperResourcePack resolves an empty FontName to MapleStory. The collector
    // maps this default to TUFReplay's embedded portable font.
    var setting = sanitized["Setting"] as JObject;
    if (setting == null)
    {
      setting = new JObject();
      sanitized["Setting"] = setting;
    }
    if (string.IsNullOrWhiteSpace((string)setting["FontName"]))
      setting["FontName"] = "Font/MAPLESTORY_OTF_BOLD.OTF";
    var files = new Dictionary<string, JToken>();
    files[kind == VisualKind.Keyviewer ? "KeyViewer.json" : "ResourcePack.json"] = sanitized;
    var assets = new VisualAssetCollector(_fileSystem, installation.RootPath, installation.RootPath, options: options);
    assets.Collect(sanitized);
    if (kind == VisualKind.Keyviewer)
      sanitized["Resources"] = VisualBuiltInAssets.AddJipperSprites(assets);
    else
      sanitized["Resources"] = VisualBuiltInAssets.AddJipperProgressSprite(assets);
    return VisualBundleBuilder.CreateBundle(
      kind,
      Source,
      installation.Version,
      VisualBundleBuilder.ViewportFrom(source),
      files,
      assets
    );
  }

  private static JToken SelectConfiguration(JObject source, VisualKind kind)
  {
    var selected = new JObject();
    if (source["Setting"] != null)
      selected["Setting"] = source["Setting"].DeepClone();

    JToken feature = source["Feature"];
    if (!(feature is JObject featureObject))
      return selected.Count == 0 ? source : selected;

    var selectedFeatures = new JObject();
    string[] names = { "AllColor", "Status", "BPM", "Combo", "Judgement", "TimingScale", "Attempt", "ResourceChanger" };
    if (kind == VisualKind.Keyviewer)
      CopyFeature(selectedFeatures, featureObject, "KeyViewer");
    else
      foreach (string name in names)
        CopyFeature(selectedFeatures, featureObject, name);
    selected["Feature"] = selectedFeatures;
    return selected;
  }

  private static void CopyFeature(JObject target, JObject features, string name)
  {
    if (!(features[name] is JObject featureObject))
      return;
    // Keep the original namespace and Enabled/Setting fields. The web
    // renderer uses these feature names to decide which controls are active.
    target[name] = featureObject.DeepClone();
  }
}
