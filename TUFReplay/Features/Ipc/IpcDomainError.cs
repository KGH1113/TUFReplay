namespace TUFReplay.Features.Ipc;

public static class IpcDomainError
{
  public static object Create(string code, string message)
  {
    return new
    {
      error = new { code, message }
    };
  }
}
