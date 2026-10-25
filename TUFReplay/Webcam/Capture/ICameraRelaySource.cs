using System;

namespace TUFReplay.Webcam.Capture;

// A running camera relay that a run's recording attaches to: the FFmpeg capture
// session in the game, or a synthetic capture in tests.
internal interface ICameraRelaySource
{
  CameraRelayLibrary Relay { get; }
  string Error { get; }

  // Excludes frames before boundaryTicks; may reuse a fresh latest frame.
  IntPtr AttachRecording(long boundaryTicks, int frameRate, int queueCapacity);
}
