using System.Text;

namespace TUFReplay.Shared.Capture;

public static class CaptureProcessArguments
{
  public static string Quote(string value)
  {
    var result = new StringBuilder("\"");
    int slashes = 0;
    foreach (char character in value)
    {
      if (character == '\\')
      {
        slashes++;
        continue;
      }
      result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
      result.Append(character);
      slashes = 0;
    }
    result.Append('\\', slashes * 2);
    return result.Append('"').ToString();
  }
}
