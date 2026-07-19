namespace TUFReplay.Infrastructure.NativeInput;

public sealed class NoopNativeInputEmitter : INativeInputEmitter
{
  public bool IsSupported(int key)
  {
    return false;
  }

  public bool EmitBatch(NativeInputEmission[] emissions, int count)
  {
    return false;
  }
}
