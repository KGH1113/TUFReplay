using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Microphone.Playback;

// The microphone volume row in ADOFAI's audio settings. The row value is in
// 0.1 dB steps, so volumes saved by older builds keep their exact value, and one
// step below the minimum is Off.
public static class MicrophoneVolumeMenu
{
  public const string RowName = "tufReplayMicrophoneVolume";
  public const string LabelKey = "pauseMenu.settings." + RowName;
  public const string DescriptionKey = "pauseMenu.settings.info." + RowName;

  public const int MinStep = (int)(MicrophoneGain.MinDecibels * StepsPerDecibel);
  public const int MaxStep = (int)(MicrophoneGain.MaxDecibels * StepsPerDecibel);
  public const int OffStep = MinStep - 1;

  // Arrow keys move 5 dB; with Shift, 1 dB.
  public const int ChangeBy = 5 * StepsPerDecibel;
  public const int ChangeBySmall = 1 * StepsPerDecibel;

  private const int StepsPerDecibel = 10;
  private const string AudioTab = "audio";
  private const string InsertAfterRow = "interfaceVolume";

  public static int ToStep(MicrophoneVolume volume) =>
    volume.Off ? OffStep : (int)Math.Round(volume.Decibels * StepsPerDecibel, MidpointRounding.AwayFromZero);

  public static MicrophoneVolume FromStep(int step) =>
    step <= OffStep
      ? MicrophoneVolume.Create(MicrophoneGain.MinDecibels, off: true)
      : MicrophoneVolume.Create(Math.Min(step, MaxStep) / (double)StepsPerDecibel, off: false);

  // Stops at the minimum before turning off, so a 5 dB press never skips past it.
  public static int Move(int step, int delta)
  {
    if (delta == 0)
      return step;
    if (step <= OffStep)
      return delta > 0 ? MinStep : OffStep;
    if (delta < 0 && step <= MinStep)
      return OffStep;
    return Math.Max(MinStep, Math.Min(MaxStep, step + delta));
  }

  public static string FormatValue(int step, bool korean)
  {
    if (step <= OffStep)
      return korean ? "끔" : "Off";
    double decibels = Math.Min(step, MaxStep) / (double)StepsPerDecibel;
    string number = decibels.ToString("0.#", CultureInfo.InvariantCulture);
    return (decibels > 0d ? "+" : "") + number + " dB";
  }

  // Null for keys that are not this row's, so ADOFAI looks them up as usual.
  public static string Text(string key, bool korean)
  {
    if (string.Equals(key, LabelKey, StringComparison.Ordinal))
      return korean ? "TUFReplay 마이크" : "TUFReplay Microphone";
    if (string.Equals(key, DescriptionKey, StringComparison.Ordinal))
      return korean
        ? "녹음된 마이크 소리의 음량이에요. 리플레이와 렌더에 함께 적용되고, 얼불춤의 다른 음량 설정에는 영향을 받지 않아요."
        : "How loud the recorded microphone is in replays and renders. ADOFAI's volume settings do not change it.";
    return null;
  }

  // Adds the row to ADOFAI's settings menu JSON after the interface volume.
  // Anything unexpected returns the menu unchanged.
  public static string AddRow(string menuJson)
  {
    try
    {
      JObject menu = JObject.Parse(menuJson);
      if (!(menu[AudioTab] is JArray audio))
        return menuJson;
      int insertAt = audio.Count;
      for (int i = 0; i < audio.Count; i++)
      {
        string name = (string)audio[i]?["name"];
        if (string.Equals(name, RowName, StringComparison.Ordinal))
          return menuJson;
        if (string.Equals(name, InsertAfterRow, StringComparison.Ordinal))
          insertAt = i + 1;
      }
      audio.Insert(
        insertAt,
        new JObject
        {
          ["name"] = RowName,
          ["type"] = "Int",
          ["min"] = OffStep,
          ["max"] = MaxStep,
          ["changeBy"] = ChangeBy,
          ["changeBySmall"] = ChangeBySmall,
        }
      );
      return menu.ToString(Newtonsoft.Json.Formatting.None);
    }
    catch (Exception)
    {
      return menuJson;
    }
  }
}
