using System;

namespace TUFReplay.Application.Recording;

internal sealed class HitMarginSnapshot
{
  private int[] _values;
  private bool _hasValue;

  internal int[] BufferForTesting => _values;

  public bool Matches(int[] values)
  {
    if (!_hasValue || values == null || _values.Length != values.Length)
      return false;

    for (int i = 0; i < _values.Length; i++)
    {
      if (_values[i] != values[i])
        return false;
    }

    return true;
  }

  public void Capture(int[] values)
  {
    if (values == null)
    {
      _hasValue = false;
      return;
    }

    if (_values == null || _values.Length != values.Length)
      _values = new int[values.Length];

    Array.Copy(values, _values, values.Length);
    _hasValue = true;
  }

  public void Reset()
  {
    _hasValue = false;
  }
}
