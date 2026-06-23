public class OpenRecordResult
{
  public bool Ok;
  public string Error;
  public string Status;
  public int TufLevelId;

  public static OpenRecordResult Opened(int tufLevelId) => new OpenRecordResult { Ok = true, Status = "opened", TufLevelId = tufLevelId };
  public static OpenRecordResult Downloading(int tufLevelId) => new OpenRecordResult { Ok = true, Status = "downloading", TufLevelId = tufLevelId };
  public static OpenRecordResult Fail(string error) => new OpenRecordResult { Ok = false, Error = error };
}
