namespace TUFReplay.Infrastructure.NativeInput;

public readonly struct NativeInputEmission
{
  public readonly int Key;
  public readonly bool Down;

  public NativeInputEmission(int key, bool down)
  {
    Key = key;
    Down = down;
  }
}
