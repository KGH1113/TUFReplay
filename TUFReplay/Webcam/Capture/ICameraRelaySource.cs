using System;

namespace TUFReplay.Webcam.Capture;

// A running camera relay that a run's recording attaches to: the FFmpeg capture
// session in the game, or a synthetic capture in tests.
internal interface ICameraRelaySource
{
  CameraRelayLibrary Relay { get; }
  string Error { get; }

  // A run's encoder stream that receives no frames until it begins.
  IntPtr PrepareRecording(int frameRate, int queueCapacity);

  // Excludes frames before boundaryTicks; may reuse a fresh latest frame.
  CameraRelayError BeginRecording(IntPtr recording, long boundaryTicks);
}
