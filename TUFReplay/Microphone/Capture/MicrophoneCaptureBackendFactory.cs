using TUFReplay.Microphone.Capture;
using UnityEngine;

namespace TUFReplay.Microphone.Capture;

public static class MicrophoneCaptureBackendFactory
{
  public static IMicrophoneCaptureBackend Create()
  {
    return UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      ? (IMicrophoneCaptureBackend)new MacOsMicrophoneCaptureBackend()
      : new UnityMicrophoneCaptureBackend();
  }
}
