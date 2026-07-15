using System.Runtime.InteropServices;
using UnityEngine;

namespace TUFReplay.Infrastructure.NativeInput;

internal static class NativeInputFocusGuardFactory
{
  public static INativeInputFocusGuard Create()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return new WindowsNativeInputFocusGuard();

    return new UnityNativeInputFocusGuard();
  }
}

internal abstract class StableNativeInputFocusGuard : INativeInputFocusGuard
{
  private const double StableDurationSeconds = 0.2d;
  private bool _trackingStableFocus;
  private double _stableSince;

  public bool IsStable(out string reason)
  {
    if (!IsForegroundTarget(out reason))
      return false;

    double now = Time.realtimeSinceStartupAsDouble;
    if (!_trackingStableFocus)
    {
      _trackingStableFocus = true;
      _stableSince = now;
      reason = "focus_stabilizing";
      return false;
    }

    if (now - _stableSince < StableDurationSeconds)
    {
      reason = "focus_stabilizing";
      return false;
    }

    reason = null;
    return true;
  }

  public bool IsForegroundTarget(out string reason)
  {
    bool safe = EvaluateForegroundTarget(out reason);
    if (!safe)
      InvalidateStability();
    return safe;
  }

  public abstract string Describe();

  protected abstract bool EvaluateForegroundTarget(out string reason);

  protected void InvalidateStability()
  {
    _trackingStableFocus = false;
    _stableSince = 0d;
  }
}

internal sealed class UnityNativeInputFocusGuard : StableNativeInputFocusGuard
{
  protected override bool EvaluateForegroundTarget(out string reason)
  {
    if (!UnityEngine.Application.isFocused)
    {
      reason = "application_not_focused";
      return false;
    }

    reason = null;
    return true;
  }

  public override string Describe()
  {
    return "unityFocused=" + UnityEngine.Application.isFocused;
  }
}
