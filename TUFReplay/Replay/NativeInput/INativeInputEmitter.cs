namespace TUFReplay.Replay.NativeInput;

public interface INativeInputEmitter
{
  bool Emit(int key, bool down);
}
