namespace TUFReplay.Application.Export;

public sealed class RunExportResult
{
  public string RunId;
  public string Outcome;
  public string FileName;
  public long ByteLength;
  public bool IncludedMicrophone;
  public string ErrorCode;
  public string Message;
}

public static class RunExportOutcomes
{
  public const string Exported = "exported";
  public const string Cancelled = "cancelled";
  public const string Error = "error";
}
