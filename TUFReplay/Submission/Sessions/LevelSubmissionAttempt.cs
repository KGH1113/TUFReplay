using System;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Submission.Capture;

namespace TUFReplay.Submission.Sessions;

/// <summary>Allocated and attached before the first input; no network work occurs here.</summary>
public sealed class LevelSubmissionAttempt
{
  public Guid RunId { get; } = Guid.NewGuid();
  public string GameplayHash { get; }
  public SubmissionRunLink Link { get; }
  private string _rejection;
  public string Failure => Volatile.Read(ref _rejection) ?? Capture.Failure;
  public EvidenceCaptureBuffer Capture { get; } = new EvidenceCaptureBuffer();
  internal readonly Stopwatch Age = Stopwatch.StartNew();
  internal volatile bool Approved;
  internal bool StartSent;
  internal bool ServerTerminal;
  private string _state = "awaiting_start";
  public string State => Volatile.Read(ref _state);
  public bool Finished => State == "sealed" || State == "unavailable";

  public LevelSubmissionAttempt(byte[] gameplayHash)
  {
    GameplayHash =
      gameplayHash?.Length == 32 ? BitConverter.ToString(gameplayHash).Replace("-", "").ToLowerInvariant() : null;
    Link = new SubmissionRunLink(RunId);
  }

  internal void Reject(string reason)
  {
    Volatile.Write(ref _rejection, reason);
    Capture.Invalidate(reason);
    SetState("unavailable");
  }

  internal void SetState(string state) => Volatile.Write(ref _state, state);
}
