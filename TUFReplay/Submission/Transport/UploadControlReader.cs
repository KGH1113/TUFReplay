using System;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Transport;

public sealed class UploadRejectedException : Exception
{
  public UploadRejectedException(string code) : base(code) { }
}

public static class UploadControlReader
{
  public static bool Apply(JObject control, UploadJournal journal)
  {
    string kind = (string)control["type"];
    if (kind == "error") throw new UploadRejectedException((string)control["code"] ?? "upload_rejected");
    if (kind != "ready" && kind != "ack" && kind != "sealed")
      throw new UploadRejectedException("unexpected_upload_response");
    if (control["acknowledged_sequence"]?.Type != JTokenType.Integer)
      throw new UploadRejectedException("invalid_acknowledgement");
    journal.Acknowledge((long)control["acknowledged_sequence"]);
    if (kind == "ready" && ((int?)control["max_chunk_bytes"] < 16384 || (int?)control["heartbeat_interval_ms"] < 1000))
      throw new UploadRejectedException("unsupported_upload_limits");
    return kind == "sealed";
  }
}
