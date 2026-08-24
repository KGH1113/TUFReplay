namespace TUFReplay.Replay.NativeInput;

public interface INativeInputEmitter
{
  bool IsSupported(int key);

  NativeInputEmitResult EmitBatch(NativeInputEmission[] emissions, int offset, int count);
}

public readonly struct NativeInputEmitResult
{
  public readonly int Emitted;
  public readonly int NativeError;

  public bool Complete(int requested) => Emitted == requested;

  public NativeInputEmitResult(int emitted, int nativeError = 0)
  {
    Emitted = emitted;
    NativeError = nativeError;
  }
}
