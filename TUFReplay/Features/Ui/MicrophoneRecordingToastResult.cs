namespace TUFReplay.Features.Ui;

public enum MicrophoneRecordingToastDecision
{
  Save,
  Discard,
}

public enum MicrophoneRecordingToastReason
{
  SaveButton,
  DiscardButton,
  Timeout,
}

public readonly struct MicrophoneRecordingToastResult
{
  public MicrophoneRecordingToastDecision Decision { get; }
  public MicrophoneRecordingToastReason Reason { get; }

  public MicrophoneRecordingToastResult(
    MicrophoneRecordingToastDecision decision,
    MicrophoneRecordingToastReason reason
  )
  {
    Decision = decision;
    Reason = reason;
  }
}
