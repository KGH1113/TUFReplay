using System;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Application.Microphone;

internal sealed class PendingMicrophoneDisposition
{
  private readonly object _gate = new object();
  private readonly Action<CapturedMicrophoneRecording, bool> _complete;
  private CapturedMicrophoneRecording _recording;
  private bool _captureComplete;
  private bool _completed;
  private bool? _persist;

  public PendingMicrophoneDisposition(Action<CapturedMicrophoneRecording, bool> complete)
  {
    _complete = complete ?? throw new ArgumentNullException(nameof(complete));
  }

  public void CompleteCapture(CapturedMicrophoneRecording recording)
  {
    bool? persist;
    lock (_gate)
    {
      if (_captureComplete)
        return;
      _recording = recording;
      _captureComplete = true;
      persist = _persist;
      if (persist.HasValue)
        _completed = true;
    }
    if (persist.HasValue)
      _complete(recording, persist.Value);
  }

  public void CompleteDisposition(bool persist)
  {
    CapturedMicrophoneRecording recording;
    lock (_gate)
    {
      if (_persist.HasValue)
        return;
      _persist = persist;
      if (!_captureComplete)
        return;
      if (_completed)
        return;
      _completed = true;
      recording = _recording;
    }
    _complete(recording, persist);
  }
}
