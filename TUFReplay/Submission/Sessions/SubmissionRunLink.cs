using System;

namespace TUFReplay.Submission.Sessions;

/// <summary>Only approved attempts become submit-capable local activity records.
/// Coordinates a short run saved before approval with the background approval callback.</summary>
public sealed class SubmissionRunLink
{
  private readonly object _sync = new object();
  private readonly string _runId;
  private bool _approved;
  private Action<string> _update;

  public SubmissionRunLink(Guid runId) => _runId = runId.ToString();

  public void Approve()
  {
    lock (_sync)
    {
      if (_approved)
        return;
      _update?.Invoke(_runId);
      _approved = true;
      _update = null;
    }
  }

  public void Save(Action<string> save, Action<string> update)
  {
    lock (_sync)
    {
      save(_approved ? _runId : null);
      if (!_approved)
        _update = update;
    }
  }
}
