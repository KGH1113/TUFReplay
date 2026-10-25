using System.Linq;
using Api = TUFReplay.Shared.Ipc.Generated;

namespace TUFReplay.Shared.Ipc;

// Explicit mappings keep domain models independent of the generated wire contract.
internal static class IpcDtoMapper
{
  public static Api.AppSession Map(TUFReplay.Activity.Ipc.ActivityAppSessionDto value) =>
    new Api.AppSession
    {
      Id = value.Id,
      StartedAtUtc = value.StartedAtUtc,
      EndedAtUtc = value.EndedAtUtc,
      RecorderTimeZoneId = value.RecorderTimeZoneId,
      RecorderUtcOffsetMinutes = value.RecorderUtcOffsetMinutes,
      LevelSessions = value.LevelSessions.Select(Map).ToArray(),
    };

  public static Api.LevelSession Map(TUFReplay.Activity.Ipc.ActivityLevelSessionOverviewDto value) =>
    new Api.LevelSession
    {
      Id = value.Id,
      LogicalLevelId = value.LogicalLevelId,
      LevelGroupId = value.LevelGroupId,
      AppSessionId = value.AppSessionId,
      TufLevelId = value.TufLevelId,
      RelativeLevelPath = value.RelativeLevelPath,
      Song = value.Song,
      Author = value.Author,
      Artist = value.Artist,
      OpenedAtUtc = value.OpenedAtUtc,
      ClosedAtUtc = value.ClosedAtUtc,
      FloorCount = value.FloorCount,
      RunCount = value.RunCount,
      ClearRunCount = value.ClearRunCount,
      NoFailRunCount = value.NoFailRunCount,
      FirstStartTile = value.FirstStartTile,
      LastStartTile = value.LastStartTile,
      ChartAvailable = value.ChartAvailable,
    };

  public static Api.LogicalLevel Map(TUFReplay.Activity.Ipc.ActivityLogicalLevelOverviewDto value) =>
    new Api.LogicalLevel
    {
      Id = value.Id,
      TufLevelId = value.TufLevelId,
      RelativeLevelPath = value.RelativeLevelPath,
      Song = value.Song,
      Author = value.Author,
      Artist = value.Artist,
      FirstSeenAtUtc = value.FirstSeenAtUtc,
      LastSeenAtUtc = value.LastSeenAtUtc,
      FloorCount = value.FloorCount,
      VisitCount = value.VisitCount,
      RunCount = value.RunCount,
      ClearRunCount = value.ClearRunCount,
      NoFailRunCount = value.NoFailRunCount,
      FirstStartTile = value.FirstStartTile,
      LastStartTile = value.LastStartTile,
      ChartAvailable = value.ChartAvailable,
    };

  public static Api.Run Map(TUFReplay.Activity.Ipc.ActivityRunDto value) =>
    new Api.Run
    {
      Id = value.Id,
      LevelSessionId = value.LevelSessionId,
      TufLevelId = value.TufLevelId,
      RunIndex = value.RunIndex,
      StartedAtUtc = value.StartedAtUtc,
      EndedAtUtc = value.EndedAtUtc,
      FloorCount = value.FloorCount,
      StartTile = value.StartTile,
      LastTile = value.LastTile,
      Result = value.Result,
      NoFailMode = value.NoFailMode,
      GameplayStartSongPosition = value.GameplayStartSongPosition,
      LevelPitchPercent = value.LevelPitchPercent,
      EffectivePitch = value.EffectivePitch,
      XAccuracy = value.XAccuracy,
      JudgmentDifficulty = value.JudgmentDifficulty,
      JudgmentSystem = value.JudgmentSystem,
      JudgmentCounts = Map(value.JudgmentCounts),
      InputCount = value.InputCount,
      HitContextCount = value.HitContextCount,
      InputBytes = value.InputBytes,
      HitContextBytes = value.HitContextBytes,
      HasMicrophoneRecording = value.HasMicrophoneRecording,
      MicrophoneRecordingBytes = value.MicrophoneRecordingBytes,
      MicrophoneDurationSeconds = value.MicrophoneDurationSeconds,
      MicrophoneSampleRate = value.MicrophoneSampleRate,
      MicrophoneChannels = value.MicrophoneChannels,
      MicrophoneRecordingPermanent = value.MicrophoneRecordingPermanent,
      MicrophoneRecordingExpiresAtUtc = value.MicrophoneRecordingExpiresAtUtc,
      HasCameraRecording = value.HasCameraRecording,
      CameraRecordingBytes = value.CameraRecordingBytes,
      CameraRecordingPermanent = value.CameraRecordingPermanent,
      CameraRecordingExpiresAtUtc = value.CameraRecordingExpiresAtUtc,
      ReplayPlayable = value.ReplayPlayable,
      ReplayUnavailableReason = value.ReplayUnavailableReason,
    };

  public static Api.JudgmentCounts Map(TUFReplay.Activity.Ipc.JudgmentCountsDto value) =>
    new Api.JudgmentCounts
    {
      Overload = value.Overload,
      TooEarly = value.TooEarly,
      Early = value.Early,
      EarlyPerfect = value.EarlyPerfect,
      Perfect = value.Perfect,
      PerfectMinus = value.PerfectMinus,
      XPerfect = value.XPerfect,
      PerfectPlus = value.PerfectPlus,
      LatePerfect = value.LatePerfect,
      Late = value.Late,
      TooLate = value.TooLate,
      Miss = value.Miss,
    };

  public static Api.LegacyReplayStatus Map(TUFReplay.Activity.Ipc.ActivityLegacyReplayStatusDto value) =>
    new Api.LegacyReplayStatus { HasLegacyReplays = value.HasLegacyReplays };

  public static Api.DeleteResult Map(TUFReplay.Activity.Ipc.ActivityRunDeleteResultDto value) =>
    new Api.DeleteResult { RunId = value.RunId, Deleted = value.Deleted };

  public static Api.ReplayState Map(TUFReplay.Replay.Ipc.ReplayPlaybackStatusDto value) =>
    new Api.ReplayState
    {
      OperationId = value.OperationId,
      RunId = value.RunId,
      State = value.State,
      ErrorCode = value.ErrorCode,
      Message = value.Message,
    };

  public static Api.LevelFileResult Map(TUFReplay.Replay.Ipc.ReplayLevelFilePickerResultDto value) =>
    new Api.LevelFileResult
    {
      OperationId = value.OperationId,
      RunId = value.RunId,
      Outcome = value.Outcome,
      LevelPath = value.LevelPath,
      ErrorCode = value.ErrorCode,
      Message = value.Message,
    };

  public static Api.MicrophoneDevice Map(TUFReplay.Microphone.Ipc.MicrophoneDeviceDto value) =>
    new Api.MicrophoneDevice
    {
      Id = value.Id,
      Name = value.Name,
      MinFrequency = value.MinFrequency,
      MaxFrequency = value.MaxFrequency,
      Bluetooth = value.Bluetooth,
    };

  public static Api.MicrophoneDevices Map(TUFReplay.Microphone.Ipc.MicrophoneDevicesResponseDto value) =>
    new Api.MicrophoneDevices
    {
      Enabled = value.Enabled,
      ToggleLocked = value.ToggleLocked,
      Devices = value.Devices.Select(Map).ToArray(),
      DefaultDeviceBluetooth = value.DefaultDeviceBluetooth,
      SelectedDeviceId = value.SelectedDeviceId,
      MicrophoneOffsetMs = value.MicrophoneOffsetMs,
      MicrophoneVolumeDb = value.MicrophoneVolumeDb,
    };

  public static Api.MicrophoneTiming Map(TUFReplay.Microphone.Ipc.MicrophoneTimingSettingsDto value) =>
    new Api.MicrophoneTiming
    {
      MicrophoneOffsetMs = value.MicrophoneOffsetMs,
      MicrophoneVolumeDb = value.MicrophoneVolumeDb,
    };

  public static Api.DeleteResult Map(TUFReplay.Microphone.Ipc.MicrophoneRecordingDeleteResultDto value) =>
    new Api.DeleteResult { RunId = value.RunId, Deleted = value.Deleted };

  public static Api.RetainResult Map(TUFReplay.Microphone.Ipc.MicrophoneRecordingKeepResultDto value) =>
    new Api.RetainResult { RunId = value.RunId, Permanent = value.Permanent };

  public static Api.DeleteResult Map(TUFReplay.Webcam.Ipc.WebcamRecordingDeleteResultDto value) =>
    new Api.DeleteResult { RunId = value.RunId, Deleted = value.Deleted };

  public static Api.RetainResult Map(TUFReplay.Webcam.Ipc.WebcamRecordingKeepResultDto value) =>
    new Api.RetainResult { RunId = value.RunId, Permanent = value.Permanent };

  public static Api.CalibrationState Map(TUFReplay.Calibration.Ipc.MicrophoneCalibrationStatusDto value) =>
    new Api.CalibrationState
    {
      OperationId = value.OperationId,
      State = value.State,
      ErrorCode = value.ErrorCode,
      Message = value.Message,
      DurationMs = value.DurationMs,
      PlaybackPositionMs = value.PlaybackPositionMs,
      ResultRevision = value.ResultRevision,
      MicrophoneOffsetMs = value.MicrophoneOffsetMs,
      MicrophoneVolumeDb = value.MicrophoneVolumeDb,
    };

  public static Api.CalibrationResult Map(TUFReplay.Calibration.Ipc.MicrophoneCalibrationResultDto value) =>
    new Api.CalibrationResult
    {
      OperationId = value.OperationId,
      Revision = value.Revision,
      DurationMs = value.DurationMs,
      GameWaveform = value.GameWaveform.Select(number => (double)number).ToArray(),
      SongWaveform = value.SongWaveform.Select(number => (double)number).ToArray(),
      MicrophoneWaveform = value.MicrophoneWaveform.Select(number => (double)number).ToArray(),
    };

  public static Api.WebcamState Map(TUFReplay.Webcam.Ipc.WebcamStateDto value) =>
    new Api.WebcamState
    {
      Supported = value.Supported,
      Backend = value.Backend,
      Enabled = value.Enabled,
      CaptureLocked = value.CaptureLocked,
      Status = value.Status,
      Error = value.Error,
      Devices = value.Devices.Select(Map).ToArray(),
      SelectedDeviceId = value.SelectedDeviceId,
      Quality = value.Quality,
      OffsetMs = value.OffsetMs,
      RecordingLimitMb = value.RecordingLimitMb,
      RetentionDays = value.RetentionDays,
      PlaybackVisible = value.PlaybackVisible,
      LiveVisible = value.LiveVisible,
      Mirror = value.Mirror,
      FlipVertical = value.FlipVertical,
      OverlayX = value.OverlayX,
      OverlayY = value.OverlayY,
      OverlayWidth = value.OverlayWidth,
      Crop = new Api.WebcamCrop
      {
        X = value.Crop.X,
        Y = value.Crop.Y,
        Width = value.Crop.Width,
        Height = value.Crop.Height,
      },
    };

  public static Api.Health Map(TUFReplay.Shared.Ipc.HealthResponseDto value) =>
    new Api.Health
    {
      Ok = value.Ok,
      Mod = value.Mod,
      ModVersion = value.ModVersion,
      ProtocolVersion = value.ProtocolVersion,
      ServerVersion = value.ServerVersion,
      BuildFlavor = value.BuildFlavor,
      AutoSubmissionProtocolVersion = value.AutoSubmissionProtocolVersion,
      ReplayEngineId = value.ReplayEngineId,
      ReplayFormatVersion = value.ReplayFormatVersion,
    };

  public static Api.WebcamDevice Map(TUFReplay.Webcam.Models.WebcamDevice value) =>
    new Api.WebcamDevice { Id = value.Id, Name = value.Name };

  public static Api.Chart Map(TUFReplay.Activity.Ipc.ActivityChartDto value) =>
    new Api.Chart
    {
      LevelSessionId = value.LevelSessionId,
      FloorCount = value.FloorCount,
      File = TUFReplay.Activity.Ipc.ActivityChartDownloads.Create(value),
    };

  public static Api.FfmpegState Map(TUFReplay.Shared.Media.FfmpegInstallState value) =>
    new Api.FfmpegState
    {
      Status = value.Status,
      Platform = value.Platform,
      Source = value.Source,
      InstallationDirectory = value.InstallationDirectory,
      Path = value.Path,
      Error = value.Error,
      DownloadedBytes = value.DownloadedBytes,
      TotalBytes = value.TotalBytes,
      Available = value.Available,
      Busy = value.Busy,
    };

  public static Api.RendererState Map(TUFReplay.Shared.Downloads.RendererInstallState value) =>
    new Api.RendererState
    {
      Status = value.Status,
      Source = value.Source,
      InstallationDirectory = value.InstallationDirectory,
      Version = value.Version,
      Error = value.Error,
      ErrorCode = value.ErrorCode,
      DownloadedBytes = value.DownloadedBytes,
      TotalBytes = value.TotalBytes,
    };
}
