using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Catalog;
using TUFReplay.Submission.Protocol;
using TUFReplay.Submission.Sessions;

namespace TUFReplay.Submission.Transport;

/// <summary>Adapts one attempt to the existing ordered ACK journal; disposal retains a healthy level socket.</summary>
internal sealed class LevelRunConnection : IUploadConnection
{
  private readonly LevelSubmissionSession _owner;
  private readonly LevelSubmissionAttempt _attempt;
  private readonly InstalledLevelContext _level;
  private readonly string _gameVersion;
  private readonly string _modVersion;
  private bool _terminal;
  private readonly bool _cleanup;

  public LevelRunConnection(
    LevelSubmissionSession owner,
    LevelSubmissionAttempt attempt,
    InstalledLevelContext level,
    string gameVersion,
    string modVersion,
    bool cleanup = false
  )
  {
    _owner = owner;
    _attempt = attempt;
    _level = level;
    _gameVersion = gameVersion;
    _modVersion = modVersion;
    _cleanup = cleanup;
  }

  private CancellationTokenSource ApprovalDeadline(CancellationToken cancellation)
  {
    var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
    if (!_attempt.Approved && !_cleanup)
    {
      var remaining = _owner.StartTimeout - _attempt.Age.Elapsed;
      if (remaining <= TimeSpan.Zero)
      {
        deadline.Dispose();
        throw new UploadRejectedException("run_start_timeout");
      }
      deadline.CancelAfter(remaining);
    }
    return deadline;
  }

  public async Task Connect(CancellationToken cancellation)
  {
    using var deadline = ApprovalDeadline(cancellation);
    await _owner.EnsureConnected(deadline.Token).ConfigureAwait(false);
  }

  public async Task SendControl(object control, CancellationToken cancellation)
  {
    JObject message = JObject.FromObject(control);
    string kind = (string)message["type"];
    if (kind == "hello")
    {
      using var deadline = ApprovalDeadline(cancellation);
      _attempt.StartSent = true;
      await _owner
        .Wire.SendControl(
          new
          {
            type = "run_start",
            run_id = _attempt.RunId,
            client_game_version = _gameVersion,
            client_mod_version = _modVersion,
            client_tuf_file_id = _level.FileId,
            client_level_relative_path = _level.RelativePath,
            last_acknowledged_sequence = (long)message["last_acknowledged_sequence"],
          },
          deadline.Token
        )
        .ConfigureAwait(false);
      return;
    }
    message["type"] =
      kind == "complete" ? "run_complete"
      : kind == "fail" ? "run_fail"
      : "run_heartbeat";
    message["run_id"] = _attempt.RunId.ToString();
    await _owner.Wire.SendControl(message, cancellation).ConfigureAwait(false);
    if (kind == "fail")
    {
      var reply = await Receive(cancellation).ConfigureAwait(false);
      if ((string)reply["type"] != "failed" && (string)reply["type"] != "sealed")
        throw new UploadRejectedException((string)reply["code"] ?? "failure_receipt_required");
      _terminal = true;
    }
  }

  public Task SendFrame(UploadFrame frame, CancellationToken cancellation) =>
    _owner.Wire.SendFrame(frame.ForRun(_attempt.RunId), cancellation);

  public async Task<JObject> Receive(CancellationToken cancellation)
  {
    using var deadline = ApprovalDeadline(cancellation);
    JObject reply = await _owner.Wire.Receive(deadline.Token).ConfigureAwait(false);
    if ((string)reply["type"] == "error")
    {
      if ((string)reply["code"] == "run_failed")
      {
        _terminal = true;
        _attempt.ServerTerminal = true;
      }
      throw new UploadRejectedException((string)reply["code"] ?? "run_rejected");
    }
    if (!Guid.TryParse((string)reply["run_id"], out Guid id) || id != _attempt.RunId)
      throw new UploadRejectedException("run_response_mismatch");
    string kind = (string)reply["type"];
    if (kind == "ready")
    {
      _attempt.Approved = true;
      if (!_cleanup)
        _attempt.SetState("uploading");
    }
    if (kind == "sealed" || kind == "failed")
    {
      _terminal = true;
      _attempt.ServerTerminal = true;
    }
    return reply;
  }

  public void Dispose()
  {
    if (!_terminal)
      _owner.ResetConnection();
  }
}
