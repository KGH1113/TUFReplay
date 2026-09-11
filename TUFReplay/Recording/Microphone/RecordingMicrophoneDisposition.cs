using TUFReplay.Composition;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Recording.Microphone;

internal static class RecordingMicrophoneDisposition
{
  public static void Complete(CapturedMicrophoneRecording recording, bool persist)
  {
    if (persist)
      FeatureRegistry.MicrophoneRecording?.Persist(recording);
    else
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
  }
}
