using UnityEngine;

namespace TUFReplay.Microphone.Playback;

// Keeps the queue filled every frame, in every game state, until the player
// unpublishes its schedule.
public sealed class ReplayMicrophoneQueueDriver : MonoBehaviour
{
  internal ReplayMicrophonePlayer Player { get; set; }

  private void Update() => Player?.Pump();
}
