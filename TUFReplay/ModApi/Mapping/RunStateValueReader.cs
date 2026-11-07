using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.ModApi.Models;

namespace TUFReplay.ModApi.Mapping;

/// <summary>
/// Reads a saved value into a type. The caller checks first with
/// <see cref="RunStateCompatibility"/> that the saved shape can be read into that type.
/// Members the saved value lacks keep the value the type's constructor gives them, and a list
/// or dictionary the constructor already created is refilled, keeping its comparer.
/// </summary>
internal static class RunStateValueReader
{
  public static object Read(RunStateType type, string json)
  {
    JToken token;
    try
    {
      using var reader = new JsonTextReader(new StringReader(json))
      {
        DateParseHandling = DateParseHandling.None,
        FloatParseHandling = FloatParseHandling.Double,
      };
      token = JToken.ReadFrom(reader);
    }
    catch (JsonException exception)
    {
      throw new RunStateValueException("The saved value is not valid JSON.", exception);
    }
    return ReadValue(type, token, null, RunStateTypeNames.Of(type.ClrType));
  }

  private static object ReadValue(RunStateType type, JToken token, object existing, string path)
  {
    if (token == null || token.Type == JTokenType.Null)
    {
      if (type.Shape.Nullable)
        return null;
      throw new RunStateValueException(
        path + " is null in the saved value, but " + RunStateTypeNames.Of(type.ClrType) + " can't be null."
      );
    }
    if (type.Underlying != null)
      return ReadValue(type.Underlying, token, null, path);

    try
    {
      switch (type.Shape.Kind)
      {
        case RunStateKind.Boolean:
          return (bool)token;
        case RunStateKind.Int32:
          return (int)token;
        case RunStateKind.Int64:
          return (long)token;
        case RunStateKind.Float32:
          return (float)token;
        case RunStateKind.Float64:
          return (double)token;
        case RunStateKind.String:
          return (string)token;
        case RunStateKind.Enum:
          return Enum.Parse(type.ClrType, (string)token, ignoreCase: false);
        case RunStateKind.DateTime:
          return DateTime.ParseExact((string)token, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        case RunStateKind.Bytes:
          return Convert.FromBase64String((string)token);
        case RunStateKind.List:
          return ReadList(type, (JArray)token, existing, path);
        case RunStateKind.Map:
          return ReadMap(type, (JObject)token, existing, path);
        case RunStateKind.Object:
          return ReadObject(type, (JObject)token, path);
      }
    }
    catch (RunStateValueException)
    {
      throw;
    }
    catch (Exception exception)
    {
      throw new RunStateValueException(
        path + " could not be read as " + RunStateTypeNames.Of(type.ClrType) + ": " + exception.Message,
        exception
      );
    }
    throw new RunStateValueException(path + " has an unknown kind.");
  }

  private static object ReadList(RunStateType type, JArray items, object existing, string path)
  {
    if (type.IsArray)
    {
      Array array = Array.CreateInstance(type.Element.ClrType, items.Count);
      for (int index = 0; index < items.Count; index++)
        array.SetValue(ReadValue(type.Element, items[index], null, path + "[" + index + "]"), index);
      return array;
    }
    var list = existing as IList ?? (IList)type.CreateInstance();
    list.Clear();
    for (int index = 0; index < items.Count; index++)
      list.Add(ReadValue(type.Element, items[index], null, path + "[" + index + "]"));
    return list;
  }

  private static object ReadMap(RunStateType type, JObject entries, object existing, string path)
  {
    var map = existing as IDictionary ?? (IDictionary)type.CreateInstance();
    map.Clear();
    foreach (JProperty entry in entries.Properties())
      map[entry.Name] = ReadValue(type.Element, entry.Value, null, path + "[\"" + entry.Name + "\"]");
    return map;
  }

  private static object ReadObject(RunStateType type, JObject json, string path)
  {
    object instance = type.CreateInstance();
    foreach (RunStateMember member in type.Members)
    {
      JToken token = json[member.Name];
      if (token == null)
        continue;
      object existing = member.Type.Shape.Kind is RunStateKind.List or RunStateKind.Map
        ? member.GetValue(instance)
        : null;
      member.SetValue(instance, ReadValue(member.Type, token, existing, path + "." + member.Name));
    }
    return instance;
  }
}
