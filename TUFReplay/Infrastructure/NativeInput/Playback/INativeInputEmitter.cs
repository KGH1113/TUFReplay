namespace TUFReplay.Infrastructure.NativeInput;

public interface INativeInputEmitter
{
  bool Emit(int key, bool down);
}
