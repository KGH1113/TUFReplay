using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Importing.Abstractions;

namespace TUFReplay.Visual.Importing.Json;

internal static class VisualJsonReader
{
  public static JObject ReadObject(IVisualFileSystem fileSystem, string path)
  {
    if (!fileSystem.FileExists(path))
      throw new VisualImportException("visual_bundle_invalid", "The visual source configuration is missing.");
    if (fileSystem.FileLength(path) > VisualImportLimits.MaxFileBytes)
      throw new VisualImportException("visual_payload_too_large", "The visual source configuration is too large.");
    try
    {
      return JObject.Parse(fileSystem.ReadAllText(path));
    }
    catch (JsonException exception)
    {
      throw new VisualImportException(
        "visual_bundle_invalid",
        "The visual source configuration is invalid.",
        exception
      );
    }
  }

  public static JToken Sanitize(JToken token)
  {
    try
    {
      return VisualJsonSanitizer.Sanitize(token);
    }
    catch (VisualImportException)
    {
      throw;
    }
    catch (Exception exception)
    {
      throw new VisualImportException(
        "visual_bundle_invalid",
        "The visual source configuration is invalid.",
        exception
      );
    }
  }
}
