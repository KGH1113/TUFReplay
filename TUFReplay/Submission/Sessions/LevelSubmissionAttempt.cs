using System;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Submission.Capture;

namespace TUFReplay.Submission.Sessions;

/// <summary>Allocated and attached before the first input; no network work occurs here.</summary>
public sealed class LevelSubmissionAttempt
{
  public Guid RunId { get; } = Guid.NewGuid();
  public EvidenceCaptureBuffer Capture { get; } = new EvidenceCaptureBuffer();
  internal readonly Stopwatch Age = Stopwatch.StartNew();
  internal bool Approved;
  internal bool StartSent;
  internal bool ServerTerminal;
  private string _state = "awaiting_start";
  public string State => Volatile.Read(ref _state);
  public bool Finished => State == "sealed" || State == "unavailable";

  internal void SetState(string state) => Volatile.Write(ref _state, state);
}
