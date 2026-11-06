using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TUFReplay.RunState.Diagnostics;
using TUFReplay.RunState.Mapping;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Providers;

/// <summary>
/// The registered providers. Registering an id again replaces its provider, as with
/// OrbitRender's OverlayApi. Readers get a stable array without taking the lock.
/// </summary>
internal static class RunStateProviders
{
  internal const int MaxIdLength = 64;

  private static readonly object Gate = new object();
  private static RunStateProvider[] _providers = Array.Empty<RunStateProvider>();
  private static bool _databaseReady;

  public static RunStateProvider[] All => Volatile.Read(ref _providers);

  public static bool Register(string id, object instance)
  {
    string problem = IdProblem(id);
    if (problem != null)
    {
      RunStateLog.Write("[RunState] A provider was not registered: " + problem);
      return false;
    }
    if (instance == null)
    {
      RunStateLog.Write("[RunState] " + id + " was not registered: pass the provider object, not null.");
      return false;
    }

    RunStateProvider provider;
    try
    {
      provider = RunStateProvider.Bind(id, instance);
    }
    catch (RunStateModelException exception)
    {
      RunStateLog.Write("[RunState] " + id + " was not registered: " + exception.Message);
      return false;
    }

    bool check;
    lock (Gate)
    {
      var providers = new List<RunStateProvider>(_providers.Where(existing => existing.Id != id)) { provider };
      Volatile.Write(ref _providers, providers.ToArray());
      check = _databaseReady;
    }
    RunStateLog.Write("[RunState] Registered " + id + " (" + RunStateTypeNames.Of(provider.Model.ClrType) + ").");
    if (check)
      RunStateDiagnostics.CheckLater(provider);
    return true;
  }

  public static void Unregister(string id)
  {
    if (string.IsNullOrEmpty(id))
      return;
    lock (Gate)
      Volatile.Write(ref _providers, _providers.Where(provider => provider.Id != id).ToArray());
  }

  public static bool IsRegistered(string id) => !string.IsNullOrEmpty(id) && All.Any(provider => provider.Id == id);

  /// <summary>True while <paramref name="provider"/> is still the one registered under its id.</summary>
  public static bool IsCurrent(RunStateProvider provider) => Array.IndexOf(All, provider) >= 0;

  /// <summary>Checks the saved shapes of every provider registered so far, and of later ones.</summary>
  public static void OnDatabaseReady()
  {
    RunStateProvider[] providers;
    lock (Gate)
    {
      _databaseReady = true;
      providers = _providers;
    }
    foreach (RunStateProvider provider in providers)
      RunStateDiagnostics.CheckLater(provider);
  }

  internal static string IdProblem(string id)
  {
    const string Example = " Use a stable id of letters, digits, '.', '_' or '-', such as \"MyMod.Attempts\".";
    if (string.IsNullOrWhiteSpace(id))
      return "the id is empty." + Example;
    if (id.Length > MaxIdLength)
      return "the id \"" + id + "\" is longer than " + MaxIdLength + " characters." + Example;
    foreach (char character in id)
      if (
        !(char.IsLetterOrDigit(character) && character < 128)
        && character != '.'
        && character != '_'
        && character != '-'
      )
        return "the id \"" + id + "\" contains '" + character + "'." + Example;
    return null;
  }

  internal static void ResetForTests()
  {
    lock (Gate)
    {
      Volatile.Write(ref _providers, Array.Empty<RunStateProvider>());
      _databaseReady = false;
    }
  }
}
