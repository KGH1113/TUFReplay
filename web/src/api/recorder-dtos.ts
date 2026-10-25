import type {
  AppSession,
  CalibrationResult,
  CalibrationState,
  DeleteResult,
  DownloadsState,
  FfmpegState,
  Health,
  JudgmentCounts,
  LegacyReplayStatus,
  LevelFileResult,
  LevelSession,
  LogicalLevel,
  MicrophoneDevice,
  MicrophoneDevices,
  MicrophoneTiming,
  RendererState,
  ReplayState,
  RetainResult,
  Run,
  WebcamCrop,
  WebcamDevice,
  WebcamState,
} from "@tuf-replay/ipc";

// Explicit generated-contract to existing application DTO mapping.
// Keep field conversion visible so contract changes are checked by TypeScript.

export function toHealthDto(value: Health) {
  return {
    AutoSubmissionProtocolVersion: value.autoSubmissionProtocolVersion,
    BuildFlavor: value.buildFlavor,
    Mod: value.mod,
    ModVersion: value.modVersion,
    Ok: value.ok,
    ProtocolVersion: value.protocolVersion,
    ReplayEngineId: value.replayEngineId,
    ReplayFormatVersion: value.replayFormatVersion,
    ServerVersion: value.serverVersion,
  };
}

export function toAppSessionDto(value: AppSession) {
  return {
    EndedAtUtc: value.endedAtUtc,
    Id: value.id,
    LevelSessions: value.levelSessions.map(toLevelSessionDto),
    RecorderTimeZoneId: value.recorderTimeZoneId,
    RecorderUtcOffsetMinutes: value.recorderUtcOffsetMinutes,
    StartedAtUtc: value.startedAtUtc,
  };
}

export function toLevelSessionDto(value: LevelSession) {
  return {
    AppSessionId: value.appSessionId,
    Artist: value.artist,
    Author: value.author,
    ChartAvailable: value.chartAvailable,
    ClearRunCount: value.clearRunCount,
    ClosedAtUtc: value.closedAtUtc,
    FirstStartTile: value.firstStartTile,
    FloorCount: value.floorCount,
    Id: value.id,
    LastStartTile: value.lastStartTile,
    LevelGroupId: value.levelGroupId,
    LogicalLevelId: value.logicalLevelId,
    NoFailRunCount: value.noFailRunCount,
    OpenedAtUtc: value.openedAtUtc,
    RelativeLevelPath: value.relativeLevelPath,
    RunCount: value.runCount,
    Song: value.song,
    TufLevelId: value.tufLevelId,
  };
}

export function toLogicalLevelDto(value: LogicalLevel) {
  return {
    Artist: value.artist,
    Author: value.author,
    ChartAvailable: value.chartAvailable,
    ClearRunCount: value.clearRunCount,
    FirstSeenAtUtc: value.firstSeenAtUtc,
    FirstStartTile: value.firstStartTile,
    FloorCount: value.floorCount,
    Id: value.id,
    LastSeenAtUtc: value.lastSeenAtUtc,
    LastStartTile: value.lastStartTile,
    NoFailRunCount: value.noFailRunCount,
    RelativeLevelPath: value.relativeLevelPath,
    RunCount: value.runCount,
    Song: value.song,
    TufLevelId: value.tufLevelId,
    VisitCount: value.visitCount,
  };
}

export function toRunDto(value: Run) {
  return {
    CameraRecordingBytes: value.cameraRecordingBytes,
    CameraRecordingExpiresAtUtc: value.cameraRecordingExpiresAtUtc,
    CameraRecordingPermanent: value.cameraRecordingPermanent,
    EffectivePitch: value.effectivePitch,
    EndedAtUtc: value.endedAtUtc,
    FloorCount: value.floorCount,
    GameplayStartSongPosition: value.gameplayStartSongPosition,
    HasCameraRecording: value.hasCameraRecording,
    HasMicrophoneRecording: value.hasMicrophoneRecording,
    HitContextBytes: value.hitContextBytes,
    HitContextCount: value.hitContextCount,
    Id: value.id,
    InputBytes: value.inputBytes,
    InputCount: value.inputCount,
    JudgmentCounts: toJudgmentCountsDto(value.judgmentCounts),
    JudgmentDifficulty: value.judgmentDifficulty,
    JudgmentSystem: value.judgmentSystem,
    LastTile: value.lastTile,
    LevelPitchPercent: value.levelPitchPercent,
    LevelSessionId: value.levelSessionId,
    MicrophoneChannels: value.microphoneChannels,
    MicrophoneDurationSeconds: value.microphoneDurationSeconds,
    MicrophoneRecordingBytes: value.microphoneRecordingBytes,
    MicrophoneRecordingExpiresAtUtc: value.microphoneRecordingExpiresAtUtc,
    MicrophoneRecordingPermanent: value.microphoneRecordingPermanent,
    MicrophoneSampleRate: value.microphoneSampleRate,
    NoFailMode: value.noFailMode,
    ReplayPlayable: value.replayPlayable,
    ReplayUnavailableReason: value.replayUnavailableReason,
    Result: value.result,
    RunIndex: value.runIndex,
    StartedAtUtc: value.startedAtUtc,
    StartTile: value.startTile,
    TufLevelId: value.tufLevelId,
    XAccuracy: value.xAccuracy,
  };
}

export function toJudgmentCountsDto(value: JudgmentCounts) {
  return {
    Early: value.early,
    EarlyPerfect: value.earlyPerfect,
    Late: value.late,
    LatePerfect: value.latePerfect,
    Miss: value.miss,
    Overload: value.overload,
    Perfect: value.perfect,
    PerfectMinus: value.perfectMinus,
    PerfectPlus: value.perfectPlus,
    TooEarly: value.tooEarly,
    TooLate: value.tooLate,
    XPerfect: value.xPerfect,
  };
}

export function toMicrophoneDevicesDto(value: MicrophoneDevices) {
  return {
    DefaultDeviceBluetooth: value.defaultDeviceBluetooth,
    Devices: value.devices.map(toMicrophoneDeviceDto),
    Enabled: value.enabled,
    MicrophoneOffsetMs: value.microphoneOffsetMs,
    MicrophoneVolumeDb: value.microphoneVolumeDb,
    SelectedDeviceId: value.selectedDeviceId,
    ToggleLocked: value.toggleLocked,
  };
}

export function toMicrophoneDeviceDto(value: MicrophoneDevice) {
  return {
    Bluetooth: value.bluetooth,
    Id: value.id,
    MaxFrequency: value.maxFrequency,
    MinFrequency: value.minFrequency,
    Name: value.name,
  };
}

export function toMicrophoneTimingDto(value: MicrophoneTiming) {
  return {
    MicrophoneOffsetMs: value.microphoneOffsetMs,
    MicrophoneVolumeDb: value.microphoneVolumeDb,
  };
}

export function toCalibrationStateDto(value: CalibrationState) {
  return {
    DurationMs: value.durationMs,
    ErrorCode: value.errorCode,
    Message: value.message,
    MicrophoneOffsetMs: value.microphoneOffsetMs,
    MicrophoneVolumeDb: value.microphoneVolumeDb,
    OperationId: value.operationId,
    PlaybackPositionMs: value.playbackPositionMs,
    ResultRevision: value.resultRevision,
    State: value.state,
  };
}

export function toCalibrationResultDto(value: CalibrationResult) {
  return {
    DurationMs: value.durationMs,
    GameWaveform: value.gameWaveform,
    MicrophoneWaveform: value.microphoneWaveform,
    OperationId: value.operationId,
    Revision: value.revision,
    SongWaveform: value.songWaveform,
  };
}

export function toReplayStateDto(value: ReplayState) {
  return {
    ErrorCode: value.errorCode,
    Message: value.message,
    OperationId: value.operationId,
    RunId: value.runId,
    State: value.state,
  };
}

export function toLevelFileResultDto(value: LevelFileResult) {
  return {
    ErrorCode: value.errorCode,
    LevelPath: value.levelPath,
    Message: value.message,
    OperationId: value.operationId,
    Outcome: value.outcome,
    RunId: value.runId,
  };
}

export function toWebcamStateDto(value: WebcamState) {
  return {
    Backend: value.backend,
    CaptureLocked: value.captureLocked,
    Crop: toWebcamCropDto(value.crop),
    Devices: value.devices.map(toWebcamDeviceDto),
    Enabled: value.enabled,
    Error: value.error,
    FlipVertical: value.flipVertical,
    LiveVisible: value.liveVisible,
    Mirror: value.mirror,
    OffsetMs: value.offsetMs,
    OverlayWidth: value.overlayWidth,
    OverlayX: value.overlayX,
    OverlayY: value.overlayY,
    PlaybackVisible: value.playbackVisible,
    Quality: value.quality,
    RecordingLimitMb: value.recordingLimitMb,
    RetentionDays: value.retentionDays,
    SelectedDeviceId: value.selectedDeviceId,
    Status: value.status,
    Supported: value.supported,
  };
}

export function toWebcamDeviceDto(value: WebcamDevice) {
  return {
    Id: value.id,
    Name: value.name,
  };
}

export function toWebcamCropDto(value: WebcamCrop) {
  return {
    Height: value.height,
    Width: value.width,
    X: value.x,
    Y: value.y,
  };
}

export function toFfmpegStateDto(value: FfmpegState) {
  return {
    Available: value.available,
    Busy: value.busy,
    DownloadedBytes: value.downloadedBytes,
    Error: value.error,
    InstallationDirectory: value.installationDirectory,
    Path: value.path,
    Platform: value.platform,
    Source: value.source,
    Status: value.status,
    TotalBytes: value.totalBytes,
  };
}

export function toRendererStateDto(value: RendererState) {
  return {
    DownloadedBytes: value.downloadedBytes,
    Error: value.error,
    ErrorCode: value.errorCode,
    InstallationDirectory: value.installationDirectory,
    Source: value.source,
    Status: value.status,
    TotalBytes: value.totalBytes,
    Version: value.version,
  };
}

export function toDownloadsStateDto(value: DownloadsState) {
  return {
    CameraNeedsFfmpeg: value.cameraNeedsFfmpeg,
    Ffmpeg: toFfmpegStateDto(value.ffmpeg),
    Renderer: toRendererStateDto(value.renderer),
  };
}

export function toDeleteResultDto(value: DeleteResult) {
  return {
    Deleted: value.deleted,
    RunId: value.runId,
  };
}

export function toRetainResultDto(value: RetainResult) {
  return {
    Permanent: value.permanent,
    RunId: value.runId,
  };
}

export function toLegacyReplayStatusDto(value: LegacyReplayStatus) {
  return {
    HasLegacyReplays: value.hasLegacyReplays,
  };
}
