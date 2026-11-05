using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.Settings;

namespace TUFReplay.Microphone.Playback;

public static class MicrophoneVolumeService
{
  public static MicrophoneVolume Current
  {
    get
    {
      TUFReplaySetting settings = TUFReplaySettingStore.Current;
      return settings == null
        ? MicrophoneVolume.Default
        : MicrophoneVolume.Create(settings.MicrophoneVolumeDb, settings.MicrophoneVolumeOff);
    }
  }

  // Applies to the playing replay right away, so it can be tuned from the pause
  // menu. Renders read the saved value when they start.
  public static MicrophoneVolume Set(MicrophoneVolume volume)
  {
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    if (settings == null)
      return Current;
    settings.MicrophoneVolumeDb = volume.Decibels;
    settings.MicrophoneVolumeOff = volume.Off;
    ReplaySessionService.UpdateActiveMicrophoneVolume(Current);
    TUFReplaySettingStore.Save();
    return Current;
  }
}
