using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Importing.Assets;
using TUFReplay.Visual.Importing.Building;
using TUFReplay.Visual.Importing.Json;

namespace TUFReplay.Visual.Sources.DmNote;

public sealed class DmNoteVisualAdapter : IVisualSourceAdapter
{
  private readonly IVisualFileSystem _fileSystem;

  public VisualSource Source { get; }

  public DmNoteVisualAdapter(IVisualFileSystem fileSystem, VisualSource source = VisualSource.Dmnote)
  {
    if (source != VisualSource.Dmnote && source != VisualSource.ImplDmnote)
      throw new ArgumentOutOfRangeException(nameof(source));
    Source = source;
    _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
  }

  public VisualBundle Import(VisualKind kind, string presetJson = null, VisualImportOptions options = null)
  {
    if (kind != VisualKind.Keyviewer)
      throw new VisualImportException("visual_source_unsupported", "DMNote exports support keyviewer presets only.");
    if (string.IsNullOrWhiteSpace(presetJson))
      throw new VisualImportException("visual_bundle_invalid", "A DMNote preset export is required.");
    if (Encoding.UTF8.GetByteCount(presetJson) > VisualImportLimits.MaxRequestBytes)
      throw new VisualImportException("visual_payload_too_large", "The DMNote preset export is too large.");

    JObject source;
    try
    {
      source = JObject.Parse(presetJson);
    }
    catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
    {
      throw new VisualImportException("visual_bundle_invalid", "The DMNote preset export is invalid.", exception);
    }
    SelectSavedTab(source);
    ValidateSingleTab(source);
    ValidatePlacement(source);
    JToken sanitized = StripLocalMachinePaths(VisualJsonReader.Sanitize(source));
    var files = new Dictionary<string, JToken> { ["preset.json"] = sanitized };
    var embeddedImages = new HashSet<string>(StringComparer.Ordinal);
    var embeddedFonts = new HashSet<string>(StringComparer.Ordinal);
    var assets = new VisualAssetCollector(
      _fileSystem,
      null,
      null,
      embeddedImages,
      embeddedFonts,
      options,
      Source == VisualSource.ImplDmnote
    );
    VisualEmbeddedAssetSupport.Collect(sanitized, assets, embeddedImages, embeddedFonts);
    assets.SupplementEmbedded(sanitized);
    VisualEmbeddedAssetSupport.ValidateReferences(sanitized, embeddedImages, embeddedFonts, assets);
    assets.Collect(sanitized);
    sanitized["defaultFontName"] =
      Source == VisualSource.Dmnote
        ? VisualBuiltInAssets.AddPretendard(assets)
        : VisualBuiltInAssets.AddFont(assets, "SUIT-Regular.woff2");
    return VisualBundleBuilder.CreateBundle(
      kind,
      Source,
      ReadSourceVersion(source),
      VisualBundleBuilder.ViewportFrom(source),
      files,
      assets
    );
  }

  private static void SelectSavedTab(JObject source)
  {
    if (!(source["keys"] is JObject keys) || keys.Count == 0)
      throw new VisualImportException("visual_bundle_invalid", "The DMNote export is missing its key map.");
    JToken selection = source["selectedKeyType"] ?? source["selected_key_type"];
    string selected = selection?.Type == JTokenType.String ? selection.Value<string>() : null;
    if ((selection == null || selection.Type == JTokenType.Null || selected == "") && keys.Count == 1)
      foreach (JProperty property in keys.Properties())
        selected = property.Name;
    if (string.IsNullOrEmpty(selected) || keys.Property(selected) == null)
      throw new VisualImportException(
        "visual_selected_tab_missing",
        "Select a tab in DMNote and export the preset again."
      );
    if (!(keys[selected] is JArray))
      throw new VisualImportException("visual_bundle_invalid", "The DMNote selected key map is invalid.");

    // Project before asset validation so references from other tabs are not imported.
    foreach (
      string field in new[]
      {
        "keys",
        "keyPositions",
        "statPositions",
        "graphPositions",
        "knobPositions",
        "spritePositions",
        "sprite_positions",
        "tabCssOverrides",
        "tabCSSOverrides",
        "tab_css_overrides",
        "tabNoteSettings",
        "tabNoteOverrides",
        "tab_note_settings",
        "tab_note_overrides",
      }
    )
    {
      if (source[field] == null)
        continue;
      if (!(source[field] is JObject map))
        throw new VisualImportException("visual_bundle_invalid", "The DMNote " + field + " map is invalid.");
      source[field] =
        map.Property(selected) == null ? new JObject() : new JObject { [selected] = map[selected].DeepClone() };
    }
    foreach (
      string field in new[]
      {
        "tabs",
        "tabList",
        "tab_definitions",
        "tabDefinitions",
        "customTabs",
        "custom_tabs",
        "viewerTabs",
        "keyviewerTabs",
      }
    )
    {
      if (!(source[field] is JArray tabs))
        continue;
      var selectedTabs = new JArray();
      foreach (JToken tab in tabs)
        if (
          tab is JObject metadata
          && metadata["id"]?.Type == JTokenType.String
          && metadata["id"].Value<string>() == selected
        )
          selectedTabs.Add(tab.DeepClone());
      source[field] = selectedTabs;
    }
    foreach (string field in new[] { "tabCount", "tab_count" })
      if (source[field] != null)
        source[field] = 1;
    if (source["selectedViewerTabs"] is JObject viewers)
    {
      var selectedViewers = new JObject();
      foreach (JProperty viewer in viewers.Properties())
        if (viewer.Value.Type == JTokenType.String && viewer.Value.Value<string>() == selected)
          selectedViewers[viewer.Name] = selected;
      source["selectedViewerTabs"] = selectedViewers;
    }
    source["selectedKeyType"] = selected;
    if (source["selected_key_type"] != null)
      source["selected_key_type"] = selected;
  }

  private static void ValidateSingleTab(JObject source)
  {
    // A DMNote tab export is identified by the mode maps, rather than by
    // customTabs. Built-in tabs intentionally omit customTabs, while a full
    // preset can contain several built-in modes and therefore several keys
    // entries. The tab export command emits exactly one entry in keys.
    JToken keysToken = source["keys"];
    if (!(keysToken is JObject keys))
      throw new VisualImportException("visual_bundle_invalid", "The DMNote export is missing its key map.");

    var modeIds = new List<string>();
    foreach (JProperty mode in keys.Properties())
    {
      if (mode.Value.Type != JTokenType.Array)
        throw new VisualImportException("visual_bundle_invalid", "The DMNote key map is invalid.");
      modeIds.Add(mode.Name);
    }

    if (modeIds.Count == 0)
      throw new VisualImportException("visual_bundle_invalid", "The DMNote export is missing its selected tab.");
    if (modeIds.Count > 1)
      throw new VisualImportException(
        "visual_multiple_tabs",
        "DMNote exports must contain exactly one tab. Export one tab and try again."
      );

    if (source["customTabs"] is JArray customTabs && customTabs.Count > 1)
      throw new VisualImportException(
        "visual_multiple_tabs",
        "DMNote exports must contain exactly one tab. Export one tab and try again."
      );
    if (source["tabCount"]?.Type == JTokenType.Integer && source["tabCount"].Value<int>() > 1)
      throw new VisualImportException(
        "visual_multiple_tabs",
        "DMNote exports must contain exactly one tab. Export one tab and try again."
      );

    string selected = (string)(source["selectedKeyType"] ?? source["selected_key_type"]);
    if (!string.IsNullOrWhiteSpace(selected) && !string.Equals(selected, modeIds[0], StringComparison.Ordinal))
      throw new VisualImportException("visual_bundle_invalid", "The DMNote selected tab does not match its key map.");

    ValidateModeMap(source, "keyPositions", modeIds[0]);
    ValidateModeMap(source, "statPositions", modeIds[0]);
    ValidateModeMap(source, "graphPositions", modeIds[0]);
    ValidateModeMap(source, "knobPositions", modeIds[0]);
    ValidateModeMap(source, "spritePositions", modeIds[0]);
    ValidateModeMap(source, "sprite_positions", modeIds[0]);
    RejectNestedMultipleTabs(source);
  }

  private static void ValidatePlacement(JObject source)
  {
    JToken value = source["tufReplayPlacement"];
    if (value == null)
      return;
    if (!(value is JObject placement))
      throw new VisualImportException("visual_bundle_invalid", "The keyviewer placement is invalid.");
    foreach (string axis in new[] { "x", "y" })
    {
      JToken coordinate = placement[axis];
      if (coordinate == null || (coordinate.Type != JTokenType.Integer && coordinate.Type != JTokenType.Float))
        throw new VisualImportException("visual_bundle_invalid", "The keyviewer placement requires two coordinates.");
      double number = coordinate.Value<double>();
      if (double.IsNaN(number) || double.IsInfinity(number) || number < 0 || number > (axis == "x" ? 1920 : 1080))
        throw new VisualImportException("visual_bundle_invalid", "The keyviewer placement is outside the screen.");
    }
    // Older presets omit scale and retain their original size.
    JToken scaleToken = placement["scale"];
    if (scaleToken != null)
    {
      if (scaleToken.Type != JTokenType.Integer && scaleToken.Type != JTokenType.Float)
        throw new VisualImportException("visual_bundle_invalid", "The keyviewer scale must be a number.");
      double scale = scaleToken.Value<double>();
      if (double.IsNaN(scale) || double.IsInfinity(scale) || scale < 0.1 || scale > 4)
        throw new VisualImportException("visual_bundle_invalid", "The keyviewer scale must be between 10% and 400%.");
    }
    source["viewport"] = new JObject { ["width"] = 1920, ["height"] = 1080 };
  }

  private static void ValidateModeMap(JObject source, string propertyName, string modeId)
  {
    JToken token = source[propertyName];
    if (token == null)
      return;
    if (!(token is JObject modes))
      throw new VisualImportException("visual_bundle_invalid", "The DMNote " + propertyName + " map is invalid.");

    // Empty optional maps are valid. When a map is present, it must describe
    // the same single mode as keys; accepting a second mode here would retain
    // an unintended tab even when keys happened to be filtered.
    foreach (JProperty mode in modes.Properties())
    {
      if (!string.Equals(mode.Name, modeId, StringComparison.Ordinal))
        throw new VisualImportException(
          "visual_multiple_tabs",
          "DMNote exports must contain exactly one tab. Export one tab and try again."
        );
      if (mode.Value.Type != JTokenType.Array)
        throw new VisualImportException("visual_bundle_invalid", "The DMNote " + propertyName + " map is invalid.");
    }
  }

  private static void RejectNestedMultipleTabs(JToken token)
  {
    if (token is JObject obj)
    {
      foreach (JProperty property in obj.Properties())
      {
        string lower = property.Name.ToLowerInvariant();
        if (
          (
            lower == "tabs"
            || lower == "tablist"
            || lower == "tab_definitions"
            || lower == "tabdefinitions"
            || lower == "customtabs"
            || lower == "custom_tabs"
            || lower == "viewertabs"
            || lower == "keyviewertabs"
          )
          && property.Value is JArray tabs
          && tabs.Count > 1
        )
          throw new VisualImportException(
            "visual_multiple_tabs",
            "DMNote exports must contain exactly one tab. Export one tab and try again."
          );
        if (
          (lower == "tabcount" || lower == "tab_count")
          && property.Value.Type == JTokenType.Integer
          && property.Value.Value<int>() > 1
        )
          throw new VisualImportException(
            "visual_multiple_tabs",
            "DMNote exports must contain exactly one tab. Export one tab and try again."
          );
        if (
          (
            lower == "keys"
            || lower == "keypositions"
            || lower == "statpositions"
            || lower == "graphpositions"
            || lower == "knobpositions"
            || lower == "spritepositions"
            || lower == "sprite_positions"
          )
          && property.Value is JObject modes
          && modes.Count > 1
        )
          throw new VisualImportException(
            "visual_multiple_tabs",
            "DMNote exports must contain exactly one tab. Export one tab and try again."
          );
        RejectNestedMultipleTabs(property.Value);
      }
      return;
    }
    if (token is JArray array)
      foreach (JToken item in array)
        RejectNestedMultipleTabs(item);
  }

  private string ReadSourceVersion(JObject source)
  {
    // schemaVersion is the preset schema (currently 1), not the DMNote
    // application version. Preset exports do not normally carry the latter,
    // so the importer reports the verified stable compatibility baseline.
    string version = (string)(
      source["applicationVersion"] ?? source["application_version"] ?? source["appVersion"] ?? source["app_version"]
    );
    return string.IsNullOrWhiteSpace(version) ? VisualSourceDefinitions.Get(Source).LatestVersion : version.Trim();
  }

  private static JToken StripLocalMachinePaths(JToken token, bool cssContext = false)
  {
    if (token is JObject obj)
    {
      var copy = new JObject();
      foreach (JProperty property in obj.Properties())
      {
        // Local font paths point into the exporting machine. Embedded font
        // bytes are the portable source and are validated separately above.
        if (
          string.Equals(property.Name, "localPath", StringComparison.OrdinalIgnoreCase)
          || (cssContext && string.Equals(property.Name, "path", StringComparison.OrdinalIgnoreCase))
        )
          continue;
        bool childCssContext =
          cssContext
          || string.Equals(property.Name, "customCSS", StringComparison.OrdinalIgnoreCase)
          || string.Equals(property.Name, "custom_css", StringComparison.OrdinalIgnoreCase)
          || string.Equals(property.Name, "tabCssOverrides", StringComparison.OrdinalIgnoreCase)
          || string.Equals(property.Name, "tab_css_overrides", StringComparison.OrdinalIgnoreCase);
        copy.Add(property.Name, StripLocalMachinePaths(property.Value, childCssContext));
      }
      return copy;
    }
    if (token is JArray array)
    {
      var copy = new JArray();
      foreach (JToken item in array)
        copy.Add(StripLocalMachinePaths(item, cssContext));
      return copy;
    }
    return token?.DeepClone();
  }
}
