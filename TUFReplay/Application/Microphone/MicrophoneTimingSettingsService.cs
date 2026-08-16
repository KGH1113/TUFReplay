using System;
using TUFReplay.Application.Replay;
using TUFReplay.Infrastructure.Settings;

namespace TUFReplay.Application.Microphone;

public sealed class MicrophoneTimingSettingsState
{
  public int MicrophoneOffsetMs;
  public int MicrophoneVolumeDb;
}

public static class MicrophoneTimingSettingsService
{
  public static MicrophoneTimingSettingsState GetState()
  {
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    return new MicrophoneTimingSettingsState
    {
      MicrophoneOffsetMs = settings?.MicrophoneOffsetMs ?? 0,
      MicrophoneVolumeDb = settings?.MicrophoneVolumeDb ?? 0,
    };
  }

  public static bool TrySetOffset(
    int offsetMs,
    out MicrophoneTimingSettingsState state,
    out string errorCode,
    out string errorMessage
  )
  {
    if (MicrophoneDeviceService.IsToggleLocked())
      return Locked(out state, out errorCode, out errorMessage);
    state = SetOffset(offsetMs);
    errorCode = null;
    errorMessage = null;
    return true;
  }

  public static bool TrySetVolume(
    int volumeDb,
    out MicrophoneTimingSettingsState state,
    out string errorCode,
    out string errorMessage
  )
  {
    if (MicrophoneDeviceService.IsToggleLocked())
      return Locked(out state, out errorCode, out errorMessage);
    state = SetVolume(volumeDb);
    errorCode = null;
    errorMessage = null;
    return true;
  }

  public static MicrophoneTimingSettingsState SetOffset(int offsetMs)
  {
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    if (settings == null)
      return GetState();
    settings.MicrophoneOffsetMs = Math.Max(
      TUFReplaySetting.MinMicrophoneOffsetMs,
      Math.Min(TUFReplaySetting.MaxMicrophoneOffsetMs, offsetMs)
    );
    TUFReplaySettingStore.Save();
    ReplaySessionService.UpdateActiveMicrophoneLatency(settings.MicrophoneOffsetMs);
    return GetState();
  }

  public static MicrophoneTimingSettingsState SetVolume(int volumeDb)
  {
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    if (settings == null)
      return GetState();
    settings.MicrophoneVolumeDb = Math.Max(
      TUFReplaySetting.MinMicrophoneVolumeDb,
      Math.Min(TUFReplaySetting.MaxMicrophoneVolumeDb, volumeDb)
    );
    TUFReplaySettingStore.Save();
    ReplaySessionService.UpdateActiveMicrophoneVolume(settings.MicrophoneVolumeDb);
    return GetState();
  }

  private static bool Locked(out MicrophoneTimingSettingsState state, out string errorCode, out string errorMessage)
  {
    state = GetState();
    errorCode = "microphone_timing_locked";
    errorMessage = "Finish the current run or calibration before changing microphone timing.";
    return false;
  }
}
