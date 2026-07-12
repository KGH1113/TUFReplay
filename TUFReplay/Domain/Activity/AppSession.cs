namespace TUFReplay.Domain.Activity;

public class AppSession
{
  public string Id;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public string RecorderTimeZoneId;
  public int RecorderUtcOffsetMinutes;
}
