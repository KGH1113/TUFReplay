using AdofaiIpc;

namespace TUFReplay.Shared.Ipc;

public static class IpcDomainError
{
  public static IpcCallException Create(string code, string message) => new IpcCallException(code, message);
}
