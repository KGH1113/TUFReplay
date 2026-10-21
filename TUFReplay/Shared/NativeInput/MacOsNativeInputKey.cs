namespace TUFReplay.Shared.NativeInput;

internal static class MacOsNativeInputKey
{
  // Quartz keyboard codes occupy 0..127. Keep mouse buttons in a disjoint
  // range within the existing ushort OS-native recording format.
  public const int MouseButtonBase = 128;
  public const int Capacity = 160;

  public static bool IsMouseButton(int key) => key >= MouseButtonBase && key < Capacity;
}
