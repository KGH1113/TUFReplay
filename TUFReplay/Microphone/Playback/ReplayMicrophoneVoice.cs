using System;
using UnityEngine;

namespace TUFReplay.Microphone.Playback;

// An AudioSource with its own in-memory clip. The clip is at the output sample
// rate, so Unity plays its samples unchanged and chunks join sample for sample.
internal sealed class ReplayMicrophoneVoice : IReplayMicrophoneVoice, IDisposable
{
  private readonly AudioSource _source;
  private readonly AudioClip _clip;

  public ReplayMicrophoneVoice(GameObject owner, int frames, int channels, int sampleRate)
  {
    _source = owner.AddComponent<AudioSource>();
    _source.playOnAwake = false;
    _source.loop = false;
    _source.spatialBlend = 0f;
    _source.volume = 1f;
    // ADOFAI's master volume sets AudioListener.volume. Renders mix the mic
    // without it, so in-game playback ignores it too and only follows the
    // microphone volume.
    _source.ignoreListenerVolume = true;
    _source.pitch = 1f;
    _source.priority = 0;
    _clip = AudioClip.Create("TUFReplay Replay Microphone", frames, channels, sampleRate, false);
    _source.clip = _clip;
  }

  public bool IsPlaying => _source.isPlaying;

  public int PositionFrames => _source.timeSamples;

  public void Play(float[] samples, double startDspTime)
  {
    _source.Stop();
    _source.timeSamples = 0;
    _clip.SetData(samples, 0);
    _source.PlayScheduled(startDspTime);
  }

  public void EndAt(double dspTime) => _source.SetScheduledEndTime(dspTime);

  public void Stop() => _source.Stop();

  public void Dispose()
  {
    _source.Stop();
    _source.clip = null;
    UnityEngine.Object.Destroy(_clip);
  }
}
