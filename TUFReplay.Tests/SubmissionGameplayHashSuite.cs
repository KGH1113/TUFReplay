using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Validation;

internal static class SubmissionGameplayHashSuite
{
  public static void RunAll()
  {
    var fixtures = JArray.Parse(
      File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "submission-gameplay-v1-vectors.json"))
    );
    foreach (JObject fixture in fixtures)
    {
      var chart = (JObject)fixture["chart"];
      string Hash(JObject value) =>
        Convert
          .ToHexString(
            SubmissionGameplayHash.Compute(
              (JObject)value["settings"],
              (JArray)value["angleData"],
              (JArray)value["actions"]
            )
          )
          .ToLowerInvariant();
      string original = Hash(chart);
      if (original != (string)fixture["sha256"])
        throw new Exception("Submission hash vector: " + fixture["name"]);
      var visual = (JObject)chart.DeepClone();
      visual["settings"]["trackColor"] = "00ff00";
      visual["settings"]["backgroundColor"] = "aabbcc";
      visual["decorations"] = new JArray();
      visual["actions"] = new JArray(
        ((JArray)visual["actions"]).Where(e => !SubmissionGameplayHash.IsVisualEvent((string)e["eventType"]))
      );
      if (Hash(visual) != original)
        throw new Exception("Visual-only edit changed submission identity.");
      var modified = (JObject)chart.DeepClone();
      modified["settings"]["bpm"] = 123;
      if (Hash(modified) == original)
        throw new Exception("BPM edit accepted.");
      ((JArray)modified["actions"]).Add(
        new JObject
        {
          ["floor"] = 0,
          ["eventType"] = "AutoPlayTiles",
          ["enabled"] = true,
        }
      );
      if (Hash(modified) == original)
        throw new Exception("Autoplay edit accepted.");
    }
    Console.WriteLine("Submission gameplay cross-language vectors passed.");
  }
}
