using System;
using System.Threading;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal sealed class NativeInputTransitionRingBuffer
{
  public const int Capacity = 8192;
  private const int IndexMask = Capacity - 1;

  private readonly NativeInputTransition[] _items = new NativeInputTransition[Capacity];
  private long _readSequence;
  private long _writeSequence;

  public int Count
  {
    get
    {
      long count = Volatile.Read(ref _writeSequence) - Volatile.Read(ref _readSequence);
      return count <= 0 ? 0 : (int)Math.Min(count, Capacity);
    }
  }

  public bool TryEnqueue(NativeInputTransition transition)
  {
    long writeSequence = _writeSequence;
    if (writeSequence - Volatile.Read(ref _readSequence) >= Capacity)
      return false;

    _items[(int)(writeSequence & IndexMask)] = transition;
    Volatile.Write(ref _writeSequence, writeSequence + 1);
    return true;
  }

  public int DrainTo(NativeInputTransition[] destination)
  {
    if (destination == null)
      throw new ArgumentNullException(nameof(destination));

    long readSequence = _readSequence;
    long writeSequence = Volatile.Read(ref _writeSequence);
    int count = (int)Math.Min(writeSequence - readSequence, destination.Length);

    for (int i = 0; i < count; i++)
    {
      destination[i] = _items[(int)((readSequence + i) & IndexMask)];
    }

    Volatile.Write(ref _readSequence, readSequence + count);
    return count;
  }

  public void Clear()
  {
    Volatile.Write(ref _readSequence, Volatile.Read(ref _writeSequence));
  }

  public void Reset()
  {
    Volatile.Write(ref _readSequence, 0L);
    Volatile.Write(ref _writeSequence, 0L);
  }
}
