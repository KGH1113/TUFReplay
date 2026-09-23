using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Visual.Importing.Assets;

/// <summary>
/// Imports the embedded image/font records used by DMNote-compatible exports.
/// </summary>
internal static class VisualEmbeddedAssetSupport
{
  public static void Collect(
    JToken token,
    VisualAssetCollector assets,
    HashSet<string> embeddedImages,
    HashSet<string> embeddedFonts
  )
  {
    Collect(token, assets, null, embeddedImages, embeddedFonts);
  }

  public static void ValidateReferences(
    JToken token,
    HashSet<string> embeddedImages,
    HashSet<string> embeddedFonts,
    VisualAssetCollector assets = null
  )
  {
    if (token is JObject obj)
    {
      foreach (JProperty property in obj.Properties())
      {
        string value = property.Value.Type == JTokenType.String ? property.Value.Value<string>() : null;
        ValidateLocalReference(value, embeddedImages, embeddedFonts, assets);
        if (
          string.Equals(property.Name, "customFonts", StringComparison.OrdinalIgnoreCase)
          && property.Value is JArray fonts
        )
        {
          foreach (JToken item in fonts)
            ValidateFontReference(item as JObject, embeddedFonts, assets);
        }
        ValidateReferences(property.Value, embeddedImages, embeddedFonts, assets);
      }
      return;
    }

    if (token is JArray array)
      foreach (JToken item in array)
        ValidateReferences(item, embeddedImages, embeddedFonts, assets);
  }

  private static void Collect(
    JToken token,
    VisualAssetCollector assets,
    string category,
    HashSet<string> embeddedImages,
    HashSet<string> embeddedFonts
  )
  {
    if (token is JObject obj)
    {
      foreach (JProperty property in obj.Properties())
      {
        string lower = property.Name.ToLowerInvariant();
        string nextCategory =
          lower.Contains("embeddedlocalimages", StringComparison.Ordinal) || lower == "images" ? "image"
          : lower.Contains("embeddedlocalfonts", StringComparison.Ordinal) || lower == "fonts" ? "font"
          : category;

        if (property.Value is JObject entry && nextCategory != null)
          CollectEntry(entry, assets, nextCategory, embeddedImages, embeddedFonts);
        else if (property.Value is JArray entries && nextCategory != null)
        {
          foreach (JToken item in entries)
            if (item is JObject arrayEntry)
              CollectEntry(arrayEntry, assets, nextCategory, embeddedImages, embeddedFonts);
        }
        Collect(property.Value, assets, nextCategory, embeddedImages, embeddedFonts);
      }
      return;
    }

    if (token is JArray array)
      foreach (JToken item in array)
        Collect(item, assets, category, embeddedImages, embeddedFonts);
  }

  private static void CollectEntry(
    JObject entry,
    VisualAssetCollector assets,
    string category,
    HashSet<string> embeddedImages,
    HashSet<string> embeddedFonts
  )
  {
    string data = null;
    string mediaType = null;
    string path = null;
    string extension = null;
    string id = null;

    foreach (JProperty property in entry.Properties())
    {
      if (property.Value.Type != JTokenType.String)
        continue;
      string lower = property.Name.ToLowerInvariant();
      string value = property.Value.Value<string>();
      if (lower == "data" || lower == "base64" || lower.EndsWith("base64", StringComparison.Ordinal))
        data = value;
      else if (lower == "mime" || lower == "mimetype" || lower == "media_type")
        mediaType = value;
      else if (lower == "path" || lower == "name" || lower == "filename" || lower == "file_name")
        path = value;
      else if (lower == "extension")
        extension = value;
      else if (lower == "imageid" || lower == "fontid" || lower == "id")
        id = value;
      else if (lower == "type" && value.Contains("/", StringComparison.Ordinal))
        mediaType = value;
    }

    if (string.IsNullOrWhiteSpace(data))
      return;
    if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.Contains("/", StringComparison.Ordinal))
      mediaType = MediaTypeFromExtension(category, extension);
    if (string.IsNullOrWhiteSpace(path))
      path = EmbeddedPath(category, id, extension, mediaType);

    assets.AddEmbedded(path, mediaType, data);
    if (!string.IsNullOrWhiteSpace(id))
    {
      if (category == "image")
        embeddedImages.Add(id);
      else if (category == "font")
        embeddedFonts.Add(id);
    }
  }

  private static void ValidateLocalReference(
    string value,
    HashSet<string> embeddedImages,
    HashSet<string> embeddedFonts,
    VisualAssetCollector assets
  )
  {
    if (string.IsNullOrWhiteSpace(value))
      return;
    if (value.StartsWith("dmnote-local-image://", StringComparison.OrdinalIgnoreCase))
    {
      string id = value.Substring("dmnote-local-image://".Length);
      if (string.IsNullOrWhiteSpace(id) || !embeddedImages.Contains(id))
        Missing(value, "image", assets);
    }
    else if (value.StartsWith("dmnote-local-font://", StringComparison.OrdinalIgnoreCase))
    {
      string id = value.Substring("dmnote-local-font://".Length);
      if (string.IsNullOrWhiteSpace(id) || !embeddedFonts.Contains(id))
        Missing(value, "font", assets);
    }
  }

  private static void ValidateFontReference(JObject font, HashSet<string> embeddedFonts, VisualAssetCollector assets)
  {
    if (font == null)
      return;
    string type = (string)(font["type"] ?? font["fontType"] ?? font["font_type"]);
    bool enabled = (bool?)font["enabled"] ?? false;
    if (!enabled || !string.Equals(type, "local", StringComparison.OrdinalIgnoreCase))
      return;
    string id = (string)(font["id"] ?? font["fontId"]);
    if (string.IsNullOrWhiteSpace(id) || !embeddedFonts.Contains(id))
      Missing("dmnote-local-font://" + id, "font", assets);
  }

  private static void Missing(string reference, string kind, VisualAssetCollector assets)
  {
    if (assets == null)
      throw new VisualImportException("visual_asset_missing", "A referenced DMNote asset is missing from the export.");
    assets.RequireEmbedded(reference, kind);
  }

  private static string MediaTypeFromExtension(string category, string extension)
  {
    string suffix = extension?.Trim().TrimStart('.').ToLowerInvariant();
    if (category == "font")
    {
      if (suffix == "otf")
        return "font/otf";
      if (suffix == "woff")
        return "font/woff";
      if (suffix == "woff2")
        return "font/woff2";
      return "font/ttf";
    }
    if (suffix == "jpg" || suffix == "jpeg")
      return "image/jpeg";
    if (suffix == "gif")
      return "image/gif";
    if (suffix == "webp")
      return "image/webp";
    if (suffix == "bmp")
      return "image/bmp";
    if (suffix == "svg")
      return "image/svg+xml";
    if (suffix == "ico")
      return "image/x-icon";
    if (suffix == "avif")
      return "image/avif";
    return "image/png";
  }

  private static string EmbeddedPath(string category, string id, string extension, string mediaType)
  {
    return "assets/dmnote-" + category + "/" + SafeId(id) + "." + NormalizeExtension(extension, mediaType, category);
  }

  private static string SafeId(string id)
  {
    if (string.IsNullOrWhiteSpace(id))
      return "embedded";
    var builder = new StringBuilder();
    foreach (char value in id.Trim())
    {
      if (builder.Length >= 96)
        break;
      builder.Append(char.IsLetterOrDigit(value) || value == '-' || value == '_' || value == '.' ? value : '_');
    }
    return builder.Length == 0 ? "embedded" : builder.ToString();
  }

  private static string NormalizeExtension(string extension, string mediaType, string category)
  {
    string value = extension?.Trim().TrimStart('.').ToLowerInvariant();
    if (!string.IsNullOrWhiteSpace(value))
      return value;
    if (string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
      return "jpg";
    if (string.Equals(mediaType, "image/gif", StringComparison.OrdinalIgnoreCase))
      return "gif";
    if (string.Equals(mediaType, "image/webp", StringComparison.OrdinalIgnoreCase))
      return "webp";
    if (string.Equals(mediaType, "image/bmp", StringComparison.OrdinalIgnoreCase))
      return "bmp";
    if (string.Equals(mediaType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
      return "svg";
    if (string.Equals(mediaType, "image/x-icon", StringComparison.OrdinalIgnoreCase))
      return "ico";
    if (string.Equals(mediaType, "image/avif", StringComparison.OrdinalIgnoreCase))
      return "avif";
    if (string.Equals(mediaType, "font/otf", StringComparison.OrdinalIgnoreCase))
      return "otf";
    if (string.Equals(mediaType, "font/woff", StringComparison.OrdinalIgnoreCase))
      return "woff";
    if (string.Equals(mediaType, "font/woff2", StringComparison.OrdinalIgnoreCase))
      return "woff2";
    return category == "font" ? "ttf" : "png";
  }
}
