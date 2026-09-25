using System;
using System.IO;
using ADOFAI;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Validation;

internal static class RuntimeSubmissionGameplayHash
{
  public static bool TryComputeCurrent(out byte[] hash, out string error)
  {
    hash = null;
    error = null;
    try
    {
      LevelData level = ADOBase.editor?.levelData ?? ADOBase.customLevel?.levelData;
      if (level == null || level.isOldLevel)
        throw new InvalidDataException("Legacy sprite charts are not supported.");
      var settings = new JObject
      {
        ["bpm"] = level.bpm,
        ["offset"] = level.offset,
        ["countdownTicks"] = level.countdownTicks,
        ["separateCountdownTime"] = level.separateCountdownTime,
      };
      var actions = new JArray();
      foreach (LevelEvent decoration in level.decorations)
        if (decoration.active)
          RejectHitbox(decoration);
      foreach (LevelEvent action in level.levelEvents)
      {
        if (!action.active)
          continue;
        RejectHitbox(action);
        string name = action.eventType.ToString();
        if (SubmissionGameplayHash.IsVisualEvent(name))
          continue;
        if (!SubmissionGameplayHash.IsGameplayEvent(name))
          throw new InvalidDataException("Unsupported gameplay event: " + name);
        var encoded = new JObject { ["floor"] = action.floor, ["eventType"] = name };
        foreach (JArray field in SubmissionGameplayHash.EventFields(name))
        {
          string key = (string)field[0];
          object value = action[key];
          if (value == null)
            continue;
          if ((string)field[1] == "string")
            encoded[key] = value.ToString();
          else if (value is UnityEngine.Vector2 vector)
            encoded[key] = new JArray(vector.x, vector.y);
          else
            encoded[key] = JToken.FromObject(value);
        }
        actions.Add(encoded);
      }
      hash = SubmissionGameplayHash.Compute(settings, new JArray(level.angleData), actions);
      return true;
    }
    catch (Exception exception)
    {
      error = exception.Message;
      return false;
    }
  }

  private static void RejectHitbox(LevelEvent action)
  {
    object hitbox = action["hitbox"];
    if (hitbox != null && hitbox.ToString() != "None")
      throw new InvalidDataException("Gameplay hitboxes are not supported.");
  }
}
