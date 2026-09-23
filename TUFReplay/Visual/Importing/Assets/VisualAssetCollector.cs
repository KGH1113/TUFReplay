using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Importing.Security;

namespace TUFReplay.Visual.Importing.Assets;

internal sealed class VisualAssetCollector
{
  private readonly IVisualFileSystem _fileSystem;
  private readonly string _sourceRoot;
  private readonly string _configRoot;
  private readonly ISet<string> _embeddedImageIds;
  private readonly ISet<string> _embeddedFontIds;
  private readonly List<VisualAsset> _assets = new List<VisualAsset>();
  private readonly HashSet<string> _assetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  private long _decodedBytes;
  private readonly VisualImportOptions _options;
  private readonly bool _legacyPretendard;
  private readonly ISet<string> _portableFontFamilies;
  private readonly Dictionary<string, string> _fontAliases = new Dictionary<string, string>(
    StringComparer.OrdinalIgnoreCase
  );
  private readonly HashSet<string> _embeddedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  private const string MapleStoryAssetPath = "assets/builtin/MAPLESTORY_OTF_BOLD.OTF";
  private const string MapleStoryResourceName = "TUFReplay.Visual.Assets.MAPLESTORY_OTF_BOLD.OTF";

  public VisualAssetCollector(
    IVisualFileSystem fileSystem,
    string sourceRoot,
    string configRoot,
    ISet<string> embeddedImageIds = null,
    ISet<string> embeddedFontIds = null,
    VisualImportOptions options = null,
    bool legacyPretendard = false,
    ISet<string> portableFontFamilies = null
  )
  {
    _fileSystem = fileSystem;
    _sourceRoot = string.IsNullOrWhiteSpace(sourceRoot) ? null : Path.GetFullPath(sourceRoot);
    _configRoot = string.IsNullOrWhiteSpace(configRoot) ? null : Path.GetFullPath(configRoot);
    _embeddedImageIds = embeddedImageIds;
    _embeddedFontIds = embeddedFontIds;
    _options = options ?? new VisualImportOptions();
    _legacyPretendard = legacyPretendard;
    _portableFontFamilies = portableFontFamilies;
    ReportProgress();
  }

  public IReadOnlyList<VisualAsset> Assets => _assets;

  public bool Contains(string path) => _assetPaths.Contains(path);

  public void Collect(JToken token)
  {
    IndexFontFamilies(token);
    Walk(token, null, false);
    if (token is JObject root && _fontAliases.Count > 0)
      root["fontAliases"] = JObject.FromObject(_fontAliases);
  }

  public void EnsureComplete()
  {
    _options.Progress?.Invoke(new VisualImportProgress("validating", completedAssets: _assets.Count));
    _options.EnsureComplete();
  }

  private void ReportProgress(string path = null) =>
    _options.Progress?.Invoke(
      new VisualImportProgress("processing_assets", path == null ? null : Path.GetFileName(path), _assets.Count)
    );

  public void RequireEmbedded(string reference, string kind) => _options.Missing(reference, kind);

  public void SupplementEmbedded(JToken token)
  {
    if (!(token is JObject root))
      return;
    foreach (VisualAssetUpload upload in _options.Uploads)
    {
      string reference = upload?.Reference ?? "";
      bool font = reference.StartsWith("dmnote-local-font://", StringComparison.Ordinal);
      bool image = reference.StartsWith("dmnote-local-image://", StringComparison.Ordinal);
      if (!font && !image)
        continue;
      string id = reference.Substring(reference.IndexOf("://", StringComparison.Ordinal) + 3);
      ISet<string> ids = font ? _embeddedFontIds : _embeddedImageIds;
      if (ids?.Contains(id) == true)
        continue;
      if (!TryUpload(reference, out string path))
        continue;
      VisualAsset asset = _assets.Find(item => item.Path == path);
      string property = font ? "embeddedLocalFonts" : "embeddedLocalImages";
      if (root[property] == null)
        root[property] = new JArray();
      if (!(root[property] is JArray entries))
        throw new VisualImportException("visual_bundle_invalid", "The embedded asset collection is invalid.");
      entries.Add(
        new JObject
        {
          [font ? "fontId" : "imageId"] = id,
          ["path"] = path,
          ["mimeType"] = asset.MediaType,
          ["dataBase64"] = asset.DataBase64,
        }
      );
      ids?.Add(id);
    }
  }

  private void IndexFontFamilies(JToken token)
  {
    if (token is JObject obj)
    {
      if (obj["customFonts"] is JArray fonts)
        foreach (JObject font in fonts.OfType<JObject>())
          if (
            (bool?)font["enabled"] != false
            && (
              _embeddedFontIds?.Contains((string)font["id"] ?? "") == true
              || !string.IsNullOrWhiteSpace((string)font["cssContent"])
            )
          )
          {
            string family = (string)(font["fontFamily"] ?? font["family"] ?? font["name"]);
            if (!string.IsNullOrWhiteSpace(family))
              _embeddedFamilies.Add(family);
          }
      foreach (JProperty property in obj.Properties())
        IndexFontFamilies(property.Value);
    }
    else if (token is JArray array)
      foreach (JToken item in array)
        IndexFontFamilies(item);
  }

  public void AddEmbedded(string path, string mediaType, string dataBase64)
  {
    ReportProgress(path);
    if (string.IsNullOrWhiteSpace(dataBase64))
      throw new VisualImportException("visual_asset_missing", "A referenced visual asset is empty.");
    byte[] bytes;
    try
    {
      bytes = Convert.FromBase64String(dataBase64);
    }
    catch (FormatException exception)
    {
      throw new VisualImportException("visual_bundle_invalid", "A visual asset is not valid base64.", exception);
    }
    AddBytes(path, mediaType, bytes);
  }

  private void Walk(JToken token, string propertyName, bool cssContext)
  {
    if (token is JObject obj)
    {
      string baseImage = obj["baseImage"]?.Type == JTokenType.String ? (string)obj["baseImage"] : null;
      string poseImage = obj["imageOverride"]?.Type == JTokenType.String ? (string)obj["imageOverride"] : null;
      foreach (JProperty property in obj.Properties())
      {
        bool childCssContext = cssContext || IsCssContainer(property.Name);
        Walk(property.Value, property.Name, childCssContext);
      }
      if (
        baseImage != null
        && obj["referenceNaturalSize"] is JObject reference
        && (string)reference["source"] == baseImage
      )
        reference["source"] = obj["baseImage"].DeepClone();
      if (poseImage != null && obj["imageOverrideMetrics"] is JObject metrics && (string)metrics["source"] == poseImage)
        metrics["source"] = obj["imageOverride"].DeepClone();
      return;
    }
    if (token is JArray array)
    {
      foreach (JToken item in array)
        Walk(item, propertyName, cssContext);
      return;
    }
    if (!(token is JValue value) || value.Type != JTokenType.String)
      return;

    string text = value.Value<string>();
    if (string.IsNullOrWhiteSpace(text))
      return;
    if (cssContext || string.Equals(propertyName, "cssContent", StringComparison.OrdinalIgnoreCase))
    {
      value.Value = CollectCssUrls(text);
      return;
    }
    if (!IsAssetProperty(propertyName))
      return;
    if (TryUpload(text, out string uploadedPath))
    {
      value.Value = uploadedPath;
      return;
    }
    if (
      IsFontFamilyProperty(propertyName)
      && !_embeddedFamilies.Contains(text)
      && TryBuiltinFont(text, out string builtinPath)
    )
    {
      value.Value = builtinPath;
      return;
    }
    if (IsFontFamilyProperty(propertyName) && IsBuiltInMapleStoryFont(text))
    {
      value.Value = AddBuiltInMapleStoryFont();
      return;
    }
    if (text.StartsWith("dmnote-local-image://", StringComparison.OrdinalIgnoreCase))
    {
      ValidateEmbeddedImageReference(text);
      return;
    }
    if (
      text.StartsWith("dmnote-local-font://", StringComparison.OrdinalIgnoreCase)
      || text.StartsWith("dmnote-local-sound://", StringComparison.OrdinalIgnoreCase)
    )
      return;
    if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
    {
      AddDataUrl(text, propertyName);
      return;
    }
    if (text.Contains(":", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal))
    {
      _options.Missing(text, IsFontFamilyProperty(propertyName) ? "font" : "image");
      return;
    }
    if (!LooksLikeAssetPath(text))
    {
      if (IsFontFamilyProperty(propertyName) && !IsPortableFamily(text) && !_embeddedFamilies.Contains(text))
        _options.Missing(text, "font");
      return;
    }
    string resolved = ResolvePath(text);
    if (resolved == null || !_fileSystem.FileExists(resolved))
    {
      _options.Missing(text, IsFontFamilyProperty(propertyName) ? "font" : "image");
      return;
    }
    value.Value = AddFile(text, resolved, propertyName);
  }

  private void AddDataUrl(string dataUrl, string propertyName)
  {
    int separator = dataUrl.IndexOf(",", StringComparison.Ordinal);
    if (separator <= 5 || !dataUrl.Substring(0, separator).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
      throw new VisualImportException("visual_bundle_invalid", "A visual data URL is invalid.");
    string header = dataUrl.Substring(5, separator - 5);
    int semicolon = header.IndexOf(';');
    if (semicolon <= 0)
      throw new VisualImportException("visual_bundle_invalid", "A visual data URL is invalid.");
    string mediaType = header.Substring(0, semicolon);
    AddEmbedded("assets/embedded-" + _assets.Count, mediaType, dataUrl.Substring(separator + 1));
  }

  private static bool IsBuiltInMapleStoryFont(string value) =>
    string.Equals(value, "MAPLESTORY_OTF_BOLD", StringComparison.OrdinalIgnoreCase)
    || string.Equals(value, "Font/MAPLESTORY_OTF_BOLD.OTF", StringComparison.OrdinalIgnoreCase)
    || string.Equals(value, "assets/MAPLESTORY_OTF_BOLD.OTF", StringComparison.OrdinalIgnoreCase);

  private string AddBuiltInMapleStoryFont()
  {
    if (_assetPaths.Contains(MapleStoryAssetPath))
      return MapleStoryAssetPath;
    using (Stream resource = typeof(VisualAssetCollector).Assembly.GetManifestResourceStream(MapleStoryResourceName))
    {
      if (resource == null)
        throw new VisualImportException("visual_bundle_invalid", "The built-in MapleStory font is unavailable.");
      using (var memory = new MemoryStream())
      {
        resource.CopyTo(memory);
        AddBytes(MapleStoryAssetPath, "font/otf", memory.ToArray());
      }
    }
    return MapleStoryAssetPath;
  }

  private string AddFile(string sourcePath, string path, string propertyName)
  {
    ReportProgress(path);
    long length = _fileSystem.FileLength(path);
    if (length <= 0 || length > VisualImportLimits.MaxDecodedAssetBytes)
      throw new VisualImportException("visual_payload_too_large", "A visual asset exceeds the permitted size.");
    byte[] bytes = _fileSystem.ReadAllBytes(path);
    string mediaType = DetectMediaType(bytes, sourcePath, propertyName);
    string logicalPath = LogicalPath(path);
    AddBytes(logicalPath, mediaType, bytes);
    return logicalPath;
  }

  private bool TryUpload(string reference, out string path)
  {
    path = null;
    VisualAssetUpload upload = _options.Find(reference);
    if (upload == null)
      return false;
    if (
      string.IsNullOrWhiteSpace(upload.DataBase64)
      || upload.DataBase64.Length > VisualImportLimits.MaxDecodedAssetBytes * 4L / 3 + 4
    )
      throw new VisualImportException("visual_payload_too_large", "An uploaded visual asset is empty or too large.");
    byte[] bytes;
    try
    {
      bytes = Convert.FromBase64String(upload.DataBase64);
    }
    catch (FormatException exception)
    {
      throw new VisualImportException("visual_bundle_invalid", "An uploaded asset is not valid base64.", exception);
    }
    string mediaType = DetectMediaType(bytes, "", "");
    string extension = mediaType.Substring(mediaType.IndexOf('/') + 1).Replace("svg+xml", "svg").Replace("jpeg", "jpg");
    path =
      "assets/uploaded/" + BitConverter.ToString(Sha256(bytes)).Replace("-", "").ToLowerInvariant() + "." + extension;
    AddBytes(path, mediaType, bytes);
    return true;
  }

  private static bool IsFontFamilyProperty(string name) =>
    (name?.EndsWith("FontName", StringComparison.OrdinalIgnoreCase) ?? false)
    || string.Equals(name, "FontName", StringComparison.OrdinalIgnoreCase)
    || string.Equals(name, "fontFamily", StringComparison.OrdinalIgnoreCase)
    || string.Equals(name, "font", StringComparison.OrdinalIgnoreCase);

  private bool IsPortableFamily(string value)
  {
    string family = value.Trim().Trim('\'', '"');
    if (_portableFontFamilies?.Contains(family) == true)
      return true;
    family = family.ToLowerInvariant();
    return family == "sans-serif"
      || family == "serif"
      || family == "monospace"
      || family == "system-ui"
      || family == "inherit"
      || family == "default";
  }

  private string CollectCssUrls(string css)
  {
    foreach (Match match in Regex.Matches(css, @"font-family\s*:\s*([^;}]+)", RegexOptions.IgnoreCase))
    foreach (string family in match.Groups[1].Value.Split(','))
      if (!_embeddedFamilies.Contains(family.Trim().Trim('\'', '"')))
        TryBuiltinFont(family, out _);
    int cursor = 0;
    int found = 0;
    while (cursor < css.Length)
    {
      int start = css.IndexOf("url(", cursor, StringComparison.OrdinalIgnoreCase);
      if (start < 0)
        return css;
      int end = css.IndexOf(')', start + 4);
      if (end < 0)
        throw new VisualImportException("visual_bundle_invalid", "A visual stylesheet contains an invalid asset URL.");
      if (++found > VisualImportLimits.MaxAssets)
        throw new VisualImportException("visual_payload_too_large", "The visual preset contains too many assets.");
      string reference = css.Substring(start + 4, end - start - 4).Trim().Trim('\'', '"');
      string builtinFile = VisualBuiltInAssets.DmnoteFontUrl(reference);
      if (builtinFile != null)
      {
        string replacement = "url(\"" + VisualBuiltInAssets.AddFont(this, builtinFile) + "\")";
        css = css.Substring(0, start) + replacement + css.Substring(end + 1);
        cursor = start + replacement.Length;
        continue;
      }
      if (TryUpload(reference, out string uploadedPath))
      {
        string replacement = "url(\"" + uploadedPath + "\")";
        css = css.Substring(0, start) + replacement + css.Substring(end + 1);
        cursor = start + replacement.Length;
        continue;
      }
      if (
        !string.IsNullOrWhiteSpace(reference)
        && reference.StartsWith("dmnote-local-image://", StringComparison.OrdinalIgnoreCase)
      )
      {
        ValidateEmbeddedImageReference(reference);
      }
      else if (
        !string.IsNullOrWhiteSpace(reference)
        && reference.StartsWith("dmnote-local-font://", StringComparison.OrdinalIgnoreCase)
      )
      {
        ValidateEmbeddedFontReference(reference);
      }
      else if (
        !string.IsNullOrWhiteSpace(reference)
        && reference.StartsWith("dmnote-local-", StringComparison.OrdinalIgnoreCase)
      )
      {
        // Sound and other executable local references are stripped by the
        // sanitizer and never become bundle assets.
      }
      else if (
        !string.IsNullOrWhiteSpace(reference) && reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
      )
      {
        AddDataUrl(reference, "cssContent");
      }
      else if (
        !string.IsNullOrWhiteSpace(reference)
        && (
          LooksLikeAssetPath(reference)
          || reference.Contains(":", StringComparison.Ordinal)
          || reference.StartsWith("//", StringComparison.Ordinal)
        )
      )
      {
        if (
          reference.Contains(":", StringComparison.Ordinal)
          || reference.StartsWith("//", StringComparison.Ordinal)
          || reference.Contains("?", StringComparison.Ordinal)
          || reference.Contains("#", StringComparison.Ordinal)
        )
        {
          _options.Missing(reference, "asset");
          cursor = end + 1;
          continue;
        }
        string resolved = ResolvePath(reference);
        if (resolved == null || !_fileSystem.FileExists(resolved))
          _options.Missing(reference, "asset");
        else
        {
          string logicalPath = AddFile(reference, resolved, "cssContent");
          string replacement = "url(\"" + logicalPath + "\")";
          css = css.Substring(0, start) + replacement + css.Substring(end + 1);
          cursor = start + replacement.Length;
          continue;
        }
      }
      cursor = end + 1;
    }
    return css;
  }

  private bool TryBuiltinFont(string family, out string path)
  {
    string name = VisualBuiltInAssets.DmnoteFontFile(family, _legacyPretendard);
    path = name == null ? null : VisualBuiltInAssets.AddFont(this, name);
    if (path != null)
      _fontAliases[family.Trim().Trim('\'', '"')] = path;
    return path != null;
  }

  private void ValidateEmbeddedImageReference(string reference)
  {
    if (_embeddedImageIds == null)
      return;
    string id = reference.Substring("dmnote-local-image://".Length);
    if (string.IsNullOrWhiteSpace(id) || !_embeddedImageIds.Contains(id))
      _options.Missing(reference, "image");
  }

  private void ValidateEmbeddedFontReference(string reference)
  {
    if (_embeddedFontIds == null)
      return;
    string id = reference.Substring("dmnote-local-font://".Length);
    if (string.IsNullOrWhiteSpace(id) || !_embeddedFontIds.Contains(id))
      _options.Missing(reference, "font");
  }

  private static bool IsCssContainer(string propertyName)
  {
    return string.Equals(propertyName, "cssContent", StringComparison.OrdinalIgnoreCase)
      || string.Equals(propertyName, "customCSS", StringComparison.OrdinalIgnoreCase)
      || string.Equals(propertyName, "custom_css", StringComparison.OrdinalIgnoreCase)
      || string.Equals(propertyName, "tabCssOverrides", StringComparison.OrdinalIgnoreCase)
      || string.Equals(propertyName, "tab_css_overrides", StringComparison.OrdinalIgnoreCase);
  }

  private void AddBytes(string path, string mediaType, byte[] bytes)
  {
    ReportProgress(path);
    if (bytes == null || bytes.Length == 0)
      throw new VisualImportException("visual_asset_missing", "A referenced visual image or font is empty.");
    if (bytes.Length > VisualImportLimits.MaxDecodedAssetBytes)
      throw new VisualImportException("visual_payload_too_large", "A visual asset exceeds the permitted size.");
    if (
      _assets.Count >= VisualImportLimits.MaxAssets
      || _decodedBytes > VisualImportLimits.MaxDecodedAssetBytesTotal - bytes.Length
    )
      throw new VisualImportException("visual_payload_too_large", "The visual preset contains too many assets.");
    if (!IsSupportedMedia(mediaType, bytes))
      throw new VisualImportException("visual_bundle_invalid", "A visual asset uses an unsupported media type.");
    string safePath = SafeAssetPath(path);
    string key = safePath + "\n" + Convert.ToBase64String(Sha256(bytes));
    if (_assetKeys.Contains(key))
      return;
    if (!_assetPaths.Add(safePath))
      throw new VisualImportException(
        "visual_bundle_invalid",
        "A visual asset path is duplicated with different content."
      );
    _assetKeys.Add(key);
    _assets.Add(
      new VisualAsset
      {
        Path = safePath,
        MediaType = mediaType,
        DataBase64 = Convert.ToBase64String(bytes),
      }
    );
    _decodedBytes += bytes.Length;
    ReportProgress();
  }

  private string ResolvePath(string value)
  {
    if (_sourceRoot == null || _configRoot == null)
      return null;
    if (!IsSafeRelativePath(value))
      throw new VisualImportException("visual_bundle_invalid", "Visual asset paths must be safe relative paths.");
    string normalized = value.Replace('\\', '/');
    string[] candidates =
    {
      Path.Combine(_configRoot, normalized),
      Path.Combine(_sourceRoot, normalized),
      Path.Combine(_configRoot, "Images", normalized),
      Path.Combine(_configRoot, "Fonts", normalized),
      Path.Combine(_sourceRoot, "Images", normalized),
      Path.Combine(_sourceRoot, "Fonts", normalized),
      Path.Combine(_sourceRoot, "UserData", normalized),
      Path.Combine(_sourceRoot, "UserData", "Images", normalized),
      Path.Combine(_sourceRoot, "UserData", "Fonts", normalized),
    };
    foreach (string candidate in candidates)
      if (IsUnder(candidate, _sourceRoot) && _fileSystem.FileExists(candidate))
        return candidate;
    return null;
  }

  private string LogicalPath(string path)
  {
    if (_sourceRoot == null)
      return SafeAssetPath(Path.GetFileName(path));
    string relative = Path.GetRelativePath(_sourceRoot, path).Replace('\\', '/');
    return SafeAssetPath(relative.StartsWith("../", StringComparison.Ordinal) ? Path.GetFileName(path) : relative);
  }

  private static string SafeAssetPath(string path)
  {
    if (!IsSafeRelativePath(path))
      throw new VisualImportException("visual_bundle_invalid", "Visual asset paths must be safe relative paths.");
    return path.Replace('\\', '/');
  }

  private static bool IsUnder(string path, string root)
  {
    return VisualPathPolicy.IsContained(root, path);
  }

  private static bool IsSafeRelativePath(string path)
  {
    if (
      string.IsNullOrWhiteSpace(path)
      || path.Length > 1024
      || path.IndexOf('\0') >= 0
      || Path.IsPathRooted(path)
      || path.Contains(":", StringComparison.Ordinal)
    )
      return false;
    string normalized = path.Replace('\\', '/');
    if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains("../", StringComparison.Ordinal))
      return false;
    foreach (string segment in normalized.Split('/'))
      if (segment == ".." || segment == "." || segment.Length == 0)
        return false;
    return true;
  }

  private static bool IsAssetProperty(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return false;
    string lower = name.ToLowerInvariant();
    return lower.Contains("image") || lower.Contains("font") || lower == "sprite" || lower == "texture";
  }

  private static bool LooksLikeAssetPath(string value)
  {
    string lower = value.ToLowerInvariant();
    return lower.Contains("/")
      || lower.Contains("\\")
      || lower.EndsWith(".png", StringComparison.Ordinal)
      || lower.EndsWith(".jpg", StringComparison.Ordinal)
      || lower.EndsWith(".jpeg", StringComparison.Ordinal)
      || lower.EndsWith(".gif", StringComparison.Ordinal)
      || lower.EndsWith(".webp", StringComparison.Ordinal)
      || lower.EndsWith(".bmp", StringComparison.Ordinal)
      || lower.EndsWith(".svg", StringComparison.Ordinal)
      || lower.EndsWith(".ico", StringComparison.Ordinal)
      || lower.EndsWith(".avif", StringComparison.Ordinal)
      || lower.EndsWith(".ttf", StringComparison.Ordinal)
      || lower.EndsWith(".otf", StringComparison.Ordinal)
      || lower.EndsWith(".woff", StringComparison.Ordinal)
      || lower.EndsWith(".woff2", StringComparison.Ordinal);
  }

  private static bool IsAvif(byte[] bytes)
  {
    if (bytes.Length < 16 || !EncodingEquals(bytes, "ftyp", 4))
      return false;
    long size = ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
    if (size < 16 || size > bytes.Length || size % 4 != 0)
      return false;
    for (int offset = 8; offset < size; offset += offset == 8 ? 8 : 4)
      if (EncodingEquals(bytes, "avif", offset) || EncodingEquals(bytes, "avis", offset))
        return true;
    return false;
  }

  private static string DetectMediaType(byte[] bytes, string path, string propertyName)
  {
    if (
      bytes.Length >= 6
      && bytes[0] == 0
      && bytes[1] == 0
      && bytes[2] == 1
      && bytes[3] == 0
      && (bytes[4] != 0 || bytes[5] != 0)
    )
      return "image/x-icon";
    if (IsAvif(bytes))
      return "image/avif";
    if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47)
      return "image/png";
    if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
      return "image/jpeg";
    if (bytes.Length >= 6 && (EncodingEquals(bytes, "GIF87a") || EncodingEquals(bytes, "GIF89a")))
      return "image/gif";
    if (bytes.Length >= 12 && EncodingEquals(bytes, "RIFF") && EncodingEquals(bytes, "WEBP", 8))
      return "image/webp";
    if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
      return "image/bmp";
    if (LooksLikeSafeSvg(bytes))
      return "image/svg+xml";
    if (
      (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0)
      || (bytes.Length >= 4 && EncodingEquals(bytes, "true"))
    )
      return "font/ttf";
    if (bytes.Length >= 4 && EncodingEquals(bytes, "OTTO"))
      return "font/otf";
    if (bytes.Length >= 4 && EncodingEquals(bytes, "wOFF"))
      return "font/woff";
    if (bytes.Length >= 4 && EncodingEquals(bytes, "wOF2"))
      return "font/woff2";
    throw new VisualImportException(
      "visual_bundle_invalid",
      "A visual asset does not have a supported media signature."
    );
  }

  private static bool IsSupportedMedia(string mediaType, byte[] bytes)
  {
    string detected;
    try
    {
      detected = DetectMediaType(bytes, "", "");
    }
    catch (VisualImportException)
    {
      return false;
    }
    return string.Equals(mediaType, detected, StringComparison.OrdinalIgnoreCase);
  }

  private static bool LooksLikeSafeSvg(byte[] bytes)
  {
    if (bytes.Length == 0)
      return false;
    try
    {
      string text = Encoding.UTF8.GetString(bytes);
      var settings = new XmlReaderSettings
      {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = false,
        MaxCharactersInDocument = VisualImportLimits.MaxDecodedAssetBytes,
      };
      using (var reader = XmlReader.Create(new StringReader(text), settings))
      {
        XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XElement root = document.Root;
        if (root == null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
          return false;

        foreach (XElement element in root.DescendantsAndSelf())
        {
          string elementName = element.Name.LocalName;
          if (
            string.Equals(elementName, "script", StringComparison.OrdinalIgnoreCase)
            || string.Equals(elementName, "foreignObject", StringComparison.OrdinalIgnoreCase)
          )
            return false;
          foreach (XAttribute attribute in element.Attributes())
          {
            if (attribute.IsNamespaceDeclaration)
              continue;
            string localName = attribute.Name.LocalName;
            if (
              localName.Length > 2
              && localName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
              && char.IsLetter(localName[2])
            )
              return false;
            if (ContainsUnsafeSvgReference(attribute.Value))
              return false;
          }
          foreach (XText textNode in element.Nodes().OfType<XText>())
            if (ContainsUnsafeSvgReference(textNode.Value))
              return false;
        }
        return true;
      }
    }
    catch (Exception exception)
      when (exception is XmlException || exception is InvalidOperationException || exception is ArgumentException)
    {
      return false;
    }
  }

  private static bool ContainsUnsafeSvgReference(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return false;
    string lower = value.ToLowerInvariant();
    if (
      lower.Contains("javascript:", StringComparison.Ordinal)
      || lower.Contains("vbscript:", StringComparison.Ordinal)
      || lower.Contains("http://", StringComparison.Ordinal)
      || lower.Contains("https://", StringComparison.Ordinal)
      || lower.Contains("//", StringComparison.Ordinal)
    )
      return true;
    return false;
  }

  private static byte[] Sha256(byte[] bytes)
  {
    using (var sha = SHA256.Create())
      return sha.ComputeHash(bytes);
  }

  private static bool EncodingEquals(byte[] bytes, string text, int offset = 0)
  {
    if (bytes.Length < offset + text.Length)
      return false;
    for (int i = 0; i < text.Length; i++)
      if (bytes[offset + i] != text[i])
        return false;
    return true;
  }
}
