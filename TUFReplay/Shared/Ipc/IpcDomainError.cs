namespace TUFReplay.Shared.Ipc;

public sealed class IpcDomainFailure
{
  public IpcDomainFailureDetail error;
}

public sealed class IpcDomainFailureDetail
{
  public string code;
  public string message;
}

public static class IpcDomainError
{
  public static object Create(string code, string message) =>
    new IpcDomainFailure
    {
      error = new IpcDomainFailureDetail { code = code, message = message },
    };
}
