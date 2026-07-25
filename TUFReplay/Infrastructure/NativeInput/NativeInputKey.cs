using System;

namespace TUFReplay.Infrastructure.NativeInput;

internal readonly struct NativeInputKey : IEquatable<NativeInputKey>
{
  public readonly int Key;
  public readonly bool ExtendedKey;

  public NativeInputKey(int key, bool extendedKey)
  {
    Key = key;
    ExtendedKey = extendedKey;
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
