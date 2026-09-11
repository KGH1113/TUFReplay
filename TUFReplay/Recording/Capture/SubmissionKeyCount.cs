using System.Collections.Generic;
using TUFReplay.Replay.Models;

namespace TUFReplay.Recording.Capture;

public static class SubmissionKeyCount
{
  public static int Count(List<RecordedInput> inputs, long wonTimeUs)
  {
    var keys = new HashSet<int>();
    foreach (var input in inputs)
      if (input.Down && input.TimeUs <= wonTimeUs)
        keys.Add((input.Key << 1) | (input.ExtendedKey ? 1 : 0));
    return keys.Count;
  }
}
