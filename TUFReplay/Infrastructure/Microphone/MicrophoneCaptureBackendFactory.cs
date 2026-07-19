using TUFReplay.Application.Microphone;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Infrastructure.Microphone;

public static class MicrophoneCaptureBackendFactory
{
  public static IMicrophoneCaptureBackend Create()
  {
    return UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      ? (IMicrophoneCaptureBackend)new MacOsMicrophoneCaptureBackend()
      : new UnityMicrophoneCaptureBackend();
  }
}
