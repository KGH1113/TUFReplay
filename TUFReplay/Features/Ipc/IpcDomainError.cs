namespace TUFReplay.Features.Ipc;

public static class IpcDomainError
{
  public static object Create(string error)
  {
    return new
    {
      ok = false,
      error
    };
  }
}
