using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Application.Replay;

/// <summary>
/// Contract the external renderer mod (TUFReplay-Renderer) implements to capture a replay into a
/// video file. TUFReplay itself contains no rendering code: when no bridge is registered, replays
/// behave exactly as if the renderer did not exist, and render-capture playback requests fail with
/// a clear error.
/// </summary>
public interface IRenderCaptureBridge
{
  /// <summary>True while frames are actively being captured on a virtual clock.</summary>
  bool IsCapturingActive { get; }

  /// <summary>False while the renderer still has preparation in flight; play mode is deferred.</summary>
  bool ReadyForPlayMode { get; }

  /// <summary>Called on the main thread immediately before the replay enters play mode.</summary>
  void OnReplayStarting(string operationId);

  /// <summary>Called when a render-capture replay reaches a terminal state.</summary>
  /// <param name="allowTrailingCapture">
  /// True when the game stays in play mode (clear/fail screen) so trailing seconds are worth
  /// capturing; false when the editor scene is about to replace the view.
  /// </param>
  void OnReplayTerminal(
    string operationId,
    string replayState,
    bool allowTrailingCapture,
    string message,
    string errorCode = null
  );

  /// <summary>
  /// A replayed input edge the capture path is about to skip. While capturing, recorded inputs
  /// are never emitted as OS events, so external key visualizers would otherwise see nothing;
  /// this hands them the edge instead. Called on the main thread, in timeline order, at the
  /// virtual time the edge belongs to.
  /// </summary>
  void OnReplayInput(SkyHook.KeyLabel key, bool down, long timeUs);

  /// <summary>
  /// Hands the run's microphone recording to the renderer for offline mixing. The renderer takes
  /// ownership of the temporary WAV file.
  /// </summary>
  void AttachMicrophone(
    string operationId,
    StoredMicrophoneRecording recording,
    Pcm16WaveInfo wave,
    Pcm16LimiterEnvelope limiterEnvelope
  );
}

/// <summary>Registration point for the renderer mod's <see cref="IRenderCaptureBridge"/>.</summary>
public static class RenderCaptureBridge
{
  public static IRenderCaptureBridge Current { get; private set; }

  public static bool IsCapturingActive => Current?.IsCapturingActive ?? false;

  public static void Register(IRenderCaptureBridge bridge)
  {
    Current = bridge;
    Main.Instance?.Log("[Render] Capture bridge registered: " + (bridge?.GetType().FullName ?? "null"));
  }

  public static void Unregister(IRenderCaptureBridge bridge)
  {
    if (ReferenceEquals(Current, bridge))
      Current = null;
  }
}
