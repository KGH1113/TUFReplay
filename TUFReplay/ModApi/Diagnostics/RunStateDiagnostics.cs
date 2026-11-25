using System;
using System.Collections.Generic;
using System.Threading;
using TUFReplay.ModApi.Mapping;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Providers;
using TUFReplay.ModApi.Repositories;

namespace TUFReplay.ModApi.Diagnostics;

/// <summary>
/// When a provider registers, checks every shape its start value and events were saved with
/// before. Runs it can't read, runs that look renamed, and events no <c>OnReplayEvent</c> takes
/// are reported once in the log, so the mod author learns about a missing upgrade method at load
/// instead of during a replay.
/// </summary>
internal static class RunStateDiagnostics
{
  public static void CheckLater(ReplayProvider provider)
  {
    ThreadPool.QueueUserWorkItem(_ => Check(provider));
  }

  internal static void Check(ReplayProvider provider)
  {
    try
    {
      var missing = new HashSet<string>(StringComparer.Ordinal);
      foreach (StoredRunStateShape shape in RunStateRepository.ListShapes(provider.Id))
      {
        RunValueTarget target;
        if (shape.EventType == null)
          target = provider.State;
        else
        {
          target = provider.EventNamed(shape.EventType)?.Target;
          if (target == null && provider.TakesEvents && missing.Add(shape.EventType))
            ModApiLog.Write(MissingHandler(provider, shape.EventType));
        }
        if (target == null || shape.ShapeId == target.Model.ShapeId)
          continue;
        string message = Describe(provider, shape, target.PlanFor(shape.ShapeId, shape.ShapeJson));
        if (message != null)
          ModApiLog.Write(message);
      }
    }
    catch (Exception exception)
    {
      ModApiLog.Write("[ReplayApi] " + provider.Id + ": saved shapes could not be checked. " + exception.Message);
    }
  }

  private static string MissingHandler(ReplayProvider provider, string savedName)
  {
    string name = RunStateTypeNames.Short(savedName);
    return "[ReplayApi] "
      + provider.Id
      + ": saved runs have "
      + name
      + " events, but "
      + provider.ProviderTypeName
      + " has no "
      + ReplayProvider.EventName
      + "("
      + name
      + " e, long ticks), so replays skip them.";
  }

  /// <summary>What to tell the mod author about one saved shape, or null when it reads cleanly.</summary>
  internal static string Describe(ReplayProvider provider, StoredRunStateShape shape, RunStateReadPlan plan)
  {
    string runs = shape.RunCount == 1 ? "1 saved run" : shape.RunCount + " saved runs";
    bool isEvent = shape.EventType != null;
    string subject = isEvent ? RunStateTypeNames.Short(shape.EventType) + " events in " + runs : runs;
    string upgrade = isEvent ? ReplayProvider.EventUpgradeName : ReplayProvider.UpgradeName;
    if (!plan.IsReadable)
      return "[ReplayApi] "
        + provider.Id
        + ": "
        + subject
        + " can't be read: "
        + plan.Problem
        + " Add "
        + upgrade
        + "(the old type) to "
        + provider.ProviderTypeName
        + (
          isEvent
            ? ", or replays skip those events."
            : ", or those runs' start values reach " + ReplayProvider.StateName + " as null."
        );

    RunStateCompatibility conversion = plan.Conversion;
    if (plan.Upgrade == null && conversion != null && conversion.Dropped.Count > 0 && conversion.Defaulted.Count > 0)
      return "[ReplayApi] "
        + provider.Id
        + ": "
        + subject
        + " are read without "
        + string.Join(", ", conversion.Dropped)
        + ", and "
        + string.Join(", ", conversion.Defaulted)
        + " keep their initial value. If a member was renamed, add "
        + upgrade
        + "(the old type) to "
        + provider.ProviderTypeName
        + ".";
    return null;
  }
}
