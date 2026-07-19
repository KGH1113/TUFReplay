namespace TUFReplay.Infrastructure.NativeInput;

public interface INativeInputEmitter
{
  bool IsSupported(int key);

  bool EmitBatch(NativeInputEmission[] emissions, int count);
}
