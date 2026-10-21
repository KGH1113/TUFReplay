using System;

namespace TUFReplay.Shared.Ipc;

public readonly struct IpcPagination
{
  private const int DefaultLimit = 200;
  private const int MaxLimit = 1000;

  private IpcPagination(int offset, int limit)
  {
    Offset = offset;
    Limit = limit;
  }

  public int Offset { get; }
  public int Limit { get; }

  public static IpcPagination Parse(int? requestedOffset, int? requestedLimit)
  {
    int offset = Math.Max(0, requestedOffset ?? 0);
    int limit = Math.Min(MaxLimit, Math.Max(1, requestedLimit ?? DefaultLimit));
    return new IpcPagination(offset, limit);
  }
}
