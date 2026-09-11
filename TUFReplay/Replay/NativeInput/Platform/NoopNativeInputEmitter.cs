namespace TUFReplay.Replay.NativeInput;

public sealed class NoopNativeInputEmitter : INativeInputEmitter
{
  public bool IsSupported(int key)
  {
    return false;
  }

  public NativeInputEmitResult EmitBatch(NativeInputEmission[] emissions, int offset, int count)
  {
    return new NativeInputEmitResult(0, -1);
  }
}
