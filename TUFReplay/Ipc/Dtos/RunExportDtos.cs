using TUFReplay.Application.Export;

namespace TUFReplay.Ipc.Dtos;

public sealed class RunExportResultDto
{
  public string RunId;
  public string Outcome;
  public string FileName;
  public long ByteLength;
  public bool IncludedMicrophone;

  public static RunExportResultDto From(RunExportResult result) =>
    new RunExportResultDto
    {
      RunId = result.RunId,
      Outcome = result.Outcome,
      FileName = result.FileName,
      ByteLength = result.ByteLength,
      IncludedMicrophone = result.IncludedMicrophone,
    };
}
