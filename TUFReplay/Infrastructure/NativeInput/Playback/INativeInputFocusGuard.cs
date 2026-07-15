namespace TUFReplay.Infrastructure.NativeInput;

internal interface INativeInputFocusGuard
{
  bool IsStable(out string reason);

  bool IsForegroundTarget(out string reason);

  string Describe();
}
