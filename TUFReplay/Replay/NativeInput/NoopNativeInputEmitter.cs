namespace TUFReplay.Replay.NativeInput;

public sealed class NoopNativeInputEmitter : INativeInputEmitter
{
  public bool Emit(int key, bool down)
  {
    return false;
  }
}
