using System;
using AdofaiIpc.Core;

namespace TUFReplay.Features.Ipc;

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

  public static IpcPagination Parse(IpcRequest request)
  {
    int offset = Math.Max(0, IpcParams.OptionalInt(request, "offset") ?? 0);
    int limit = Math.Min(MaxLimit, Math.Max(1, IpcParams.OptionalInt(request, "limit") ?? DefaultLimit));
    return new IpcPagination(offset, limit);
  }
}
