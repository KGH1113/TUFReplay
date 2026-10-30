using System;
using System.Reflection;
using TUFReplay.Replay.Sessions;

namespace TUFReplay.Replay.Compatibility;

/// <summary>
/// Keeps replay playback out of ImplResourcePack's attempt counter. ImplResourcePack asks this
/// rule each time a play starts, and the rule reads whether a replay is installed, so nothing has
/// to be switched back on after the replay. Optional: without ImplResourcePack, or with a version
/// that has no attempt counter, this does nothing. Resolved by reflection, like ImplResourcePack
/// resolves <see cref="NativeInputPause"/>.
/// </summary>
internal static class ImplResourcePackAttempts
{
  private const string Owner = "TUFReplay";
  private const string AssemblyName = "ImplResourcePack";
  private const string ApiTypeName = "ImplResourcePack.AttemptCounter";
  internal const int SupportedApiVersion = 1;

  internal static readonly Func<bool> SkipReplays = () => ReplaySessionService.HasActiveContext;

  private static bool _registered;
  private static bool _warned;

  /// <summary>
  /// Sets the rule if ImplResourcePack is loaded. Called before every replay starts, so mod load
  /// order does not matter; once set, this only checks a flag.
  /// </summary>
  public static void EnsureRegistered()
  {
    if (_registered)
      return;
    try
    {
      foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        if (!string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.Ordinal))
          continue;
        if (TryRegister(assembly.GetType(ApiTypeName, false)))
          _registered = true;
      }
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(ImplResourcePackAttempts), exception);
    }
  }

  /// <summary>Sets the rule through <paramref name="api"/>; false if it is missing or a different version.</summary>
  internal static bool TryRegister(Type api)
  {
    if (api == null)
      return false;

    FieldInfo version = api.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
    MethodInfo setSkipRule = api.GetMethod(
      "SetSkipRule",
      BindingFlags.Public | BindingFlags.Static,
      null,
      new[] { typeof(string), typeof(Func<bool>) },
      null
    );
    if (!(version?.GetValue(null) is int value) || value != SupportedApiVersion || setSkipRule == null)
    {
      WarnUnsupported(version?.GetValue(null));
      return false;
    }

    setSkipRule.Invoke(null, new object[] { Owner, SkipReplays });
    return true;
  }

  private static void WarnUnsupported(object version)
  {
    if (_warned)
      return;
    _warned = true;
    Main.Instance?.Log(
      "[Replay] ImplResourcePack's attempt counter API is version "
        + (version ?? "unknown")
        + ", and this TUFReplay supports "
        + SupportedApiVersion
        + ", so replays may count as attempts there. Update both mods to fix this."
    );
  }
}
