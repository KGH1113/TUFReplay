using System;
using System.Threading;
using TUFReplay.ModApi.Mapping;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Providers;
using TUFReplay.ModApi.Repositories;

namespace TUFReplay.ModApi.Diagnostics;

/// <summary>
/// When a provider registers, checks every shape its state was saved with before. Runs it
/// can't read, and runs that look renamed, are reported once in the log, so the mod author
/// learns about a missing upgrade method at load instead of during a replay.
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
      foreach (StoredRunStateShape shape in RunStateRepository.ListShapes(provider.Id))
      {
        if (shape.ShapeId == provider.Model.ShapeId)
          continue;
        string message = Describe(provider, shape, provider.PlanFor(shape.ShapeId, shape.ShapeJson));
        if (message != null)
          ModApiLog.Write(message);
      }
    }
    catch (Exception exception)
    {
      ModApiLog.Write("[ReplayApi] " + provider.Id + ": saved shapes could not be checked. " + exception.Message);
    }
  }

  internal static string Describe(ReplayProvider provider, StoredRunStateShape shape, RunStateReadPlan plan)
  {
    string runs = shape.RunCount == 1 ? "1 saved run" : shape.RunCount + " saved runs";
    if (!plan.IsReadable)
      return "[ReplayApi] "
        + provider.Id
        + ": "
        + runs
        + " can't be read: "
        + plan.Problem
        + " Add "
        + ReplayProvider.UpgradeName
        + "(the old type) to "
        + provider.ProviderTypeName
        + ", or those runs' values reach "
        + ReplayProvider.EventName
        + " as null or are skipped.";

    RunStateCompatibility conversion = plan.Conversion;
    if (plan.Upgrade == null && conversion != null && conversion.Dropped.Count > 0 && conversion.Defaulted.Count > 0)
      return "[ReplayApi] "
        + provider.Id
        + ": "
        + runs
        + " are read without "
        + string.Join(", ", conversion.Dropped)
        + ", and "
        + string.Join(", ", conversion.Defaulted)
        + " keep their initial value. If a member was renamed, add "
        + ReplayProvider.UpgradeName
        + "(the old type) to "
        + provider.ProviderTypeName
        + ".";
    return null;
  }
}
