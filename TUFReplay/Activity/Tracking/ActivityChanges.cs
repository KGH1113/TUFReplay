using System;

namespace TUFReplay.Activity.Tracking;

/// <summary>Raised after persisted activity changes; delivery belongs to composition adapters.</summary>
public static class ActivityChanges
{
  public static event Action<string> Changed;

  public static void Notify(string runId = null) => Changed?.Invoke(runId);
}
