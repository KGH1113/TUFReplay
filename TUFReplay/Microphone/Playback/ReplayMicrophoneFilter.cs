using System;
using UnityEngine;

namespace TUFReplay.Microphone.Playback;

// Unity calls this on the audio thread for every block the replay microphone's
// carrier source mixes. The renderer replaces the carrier's silence with the
// recording. AudioSettings.dspTime here is the DSP time of the block, the
// clock ADOFAI schedules its song on.
public sealed class ReplayMicrophoneFilter : MonoBehaviour
{
  private volatile ReplayMicrophoneRenderer _renderer;

  internal ReplayMicrophoneRenderer Renderer
  {
    set => _renderer = value;
  }

  private void OnAudioFilterRead(float[] data, int channels)
  {
    ReplayMicrophoneRenderer renderer = _renderer;
    if (renderer == null)
    {
      Array.Clear(data, 0, data.Length);
      return;
    }
    renderer.Render(data, channels, AudioSettings.dspTime);
  }
}
