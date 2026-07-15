using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Domain.Activity;

namespace TUFReplay.Infrastructure.Adofai;

public static class AdofaiLevelMetadataReader
{
  public static bool TryRead(string levelPath, out LevelMetadataSnapshot metadata)
  {
    metadata = null;
    if (string.IsNullOrWhiteSpace(levelPath) || !File.Exists(levelPath))
      return false;

    try
    {
      using StreamReader stream = File.OpenText(levelPath);
      using JsonTextReader reader = new JsonTextReader(stream);
      while (reader.Read())
      {
        if (
          reader.TokenType != JsonToken.PropertyName
          || !string.Equals(reader.Value as string, "settings", StringComparison.Ordinal)
        )
        {
          continue;
        }

        if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
          return false;

        JObject settings = JObject.Load(reader);
        metadata = new LevelMetadataSnapshot
        {
          Song = settings.Value<string>("song"),
          Author = settings.Value<string>("author"),
          Artist = settings.Value<string>("artist"),
        };
        return true;
      }
    }
    catch (Exception ex)
      when (ex is IOException
        || ex is UnauthorizedAccessException
        || ex is JsonException
        || ex is ArgumentException
      )
    {
      return false;
    }

    return false;
  }
}
