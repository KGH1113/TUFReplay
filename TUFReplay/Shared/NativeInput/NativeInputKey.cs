using System;

namespace TUFReplay.Shared.NativeInput;

internal readonly struct NativeInputKey : IEquatable<NativeInputKey>
{
  public readonly int Key;
  public readonly bool ExtendedKey;
  public readonly int NativeCode;
  public readonly ulong NativeFlags;

  public NativeInputKey(int key, bool extendedKey, int nativeCode = -1, ulong nativeFlags = 0)
  {
    Key = key;
    ExtendedKey = extendedKey;
    NativeCode = nativeCode;
    NativeFlags = nativeFlags;
  }

  public bool Equals(NativeInputKey other)
  {
    return Key == other.Key && ExtendedKey == other.ExtendedKey;
  }

  public override bool Equals(object obj)
  {
    return obj is NativeInputKey other && Equals(other);
  }

  public override int GetHashCode()
  {
    return (Key * 397) ^ (ExtendedKey ? 1 : 0);
  }
}
