using System;

namespace TUFReplay.Infrastructure.Settings;

public static class TUFReplaySettingStore
{
  private static string _path;

  public static TUFReplaySetting Current { get; private set; }

  public static void Initialize(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
      throw new ArgumentException("A settings path is required.", nameof(path));

    _path = path;
    Current = TUFReplaySetting.Load(path);
  }

  public static void Save()
  {
    if (Current == null || string.IsNullOrWhiteSpace(_path))
      throw new InvalidOperationException("The TUFReplay settings store has not been initialized.");
    Current.Save(_path);
  }
}
