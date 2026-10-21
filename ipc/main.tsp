import "@adofai-ipc/typespec";

@ipc.mod("tuf-replay")
namespace ReplayApi {
  model Health {
    ok: boolean; mod: "TUFReplay"; modVersion: string; protocolVersion: int32;
    serverVersion: int32; buildFlavor: "standard" | "auto-submission";
    autoSubmissionProtocolVersion: int32; replayEngineId: string; replayFormatVersion: int32;
  }
  model AppSession {
    id: string; startedAtUtc: string; endedAtUtc: string | null;
    recorderTimeZoneId: string | null; recorderUtcOffsetMinutes: int32;
    levelSessions: LevelSession[];
  }
  model LevelSession {
    id: string; logicalLevelId: string; levelGroupId: string; appSessionId: string;
    tufLevelId: int32 | null; relativeLevelPath: string | null;
    song: string | null; author: string | null; artist: string | null;
    openedAtUtc: string; closedAtUtc: string | null; floorCount: int32; runCount: int32;
    clearRunCount: int32; noFailRunCount: int32; firstStartTile: int32 | null;
    lastStartTile: int32 | null; chartAvailable: boolean;
  }
  model LogicalLevel {
    id: string; tufLevelId: int32 | null; relativeLevelPath: string | null;
    song: string | null; author: string | null; artist: string | null;
    firstSeenAtUtc: string; lastSeenAtUtc: string; floorCount: int32; visitCount: int32;
    runCount: int32; clearRunCount: int32; noFailRunCount: int32;
    firstStartTile: int32 | null; lastStartTile: int32 | null; chartAvailable: boolean;
  }
  model JudgmentCounts {
    overload: int32; tooEarly: int32; early: int32; earlyPerfect: int32;
    perfect: int32; perfectMinus: int32; xPerfect: int32; perfectPlus: int32;
    latePerfect: int32; late: int32; tooLate: int32; miss: int32;
  }
  model Run {
    id: string; levelSessionId: string; tufLevelId: int32 | null; runIndex: int32;
    startedAtUtc: string; endedAtUtc: string | null; floorCount: int32;
    startTile: int32; lastTile: int32 | null; result: string; noFailMode: boolean;
    gameplayStartSongPosition: float64 | null; levelPitchPercent: int32 | null;
    effectivePitch: float64 | null; xAccuracy: float64 | null;
    judgmentDifficulty: "Lenient" | "Normal" | "Strict" | null;
    judgmentSystem: "Legacy" | "ModernClassic" | "ModernCompetitive";
    judgmentCounts: JudgmentCounts; inputCount: int32; hitContextCount: int32;
    // SQLite BLOB lengths and bounded recording lengths are exact safe integers.
    inputBytes: safeint; hitContextBytes: safeint; hasMicrophoneRecording: boolean;
    microphoneRecordingBytes: safeint; microphoneDurationSeconds: float64 | null;
    microphoneSampleRate: int32 | null; microphoneChannels: int32 | null;
    microphoneRecordingPermanent: boolean; microphoneRecordingExpiresAtUtc: string | null;
    hasCameraRecording: boolean; cameraRecordingBytes: safeint;
    cameraRecordingPermanent: boolean; cameraRecordingExpiresAtUtc: string | null;
    replayPlayable: boolean; replayUnavailableReason: string | null;
  }
  model Chart { levelSessionId: string; floorCount: int32; file: ipc.File; }
  model LegacyReplayStatus { hasLegacyReplays: boolean; }
  model DeleteResult { runId: string; deleted: boolean; }
  model RetainResult { runId: string; permanent: boolean; }
  model ActivityChanged { revision: safeint; runId: string | null; }
  model ReplayState {
    operationId: string | null; runId: string | null;
    state: "idle" | "preparing" | "opening_level" | "waiting_for_focus" | "starting" | "playing" | "returning_to_editor" | "completed" | "cancelled" | "error";
    errorCode: string | null; message: string | null;
  }
  model LevelFileResult {
    operationId: string | null; runId: string | null;
    outcome: "picking" | "selected" | "mismatch" | "cancelled" | "error";
    levelPath: string | null; errorCode: string | null; message: string | null;
  }
  model MicrophoneDevice { id: string; name: string; minFrequency: int32; maxFrequency: int32; }
  model MicrophoneDevices {
    enabled: boolean; toggleLocked: boolean; devices: MicrophoneDevice[];
    selectedDeviceId: string | null; microphoneOffsetMs: int32; microphoneVolumeDb: int32;
  }
  model MicrophoneTiming { microphoneOffsetMs: int32; microphoneVolumeDb: int32; }
  model CalibrationState {
    operationId: string | null;
    state: "idle" | "arming" | "opening_level" | "waiting_for_run" | "recording" | "processing" | "editing" | "preview_starting" | "preview_playing" | "error";
    errorCode: string | null; message: string | null; durationMs: float64;
    playbackPositionMs: float64; resultRevision: int32;
    microphoneOffsetMs: int32; microphoneVolumeDb: int32;
  }
  model CalibrationResult {
    operationId: string; revision: int32; durationMs: float64;
    gameWaveform: float64[]; songWaveform: float64[]; microphoneWaveform: float64[];
  }
  model WebcamDevice { id: string; name: string; }
  model WebcamCrop { x: float64; y: float64; width: float64; height: float64; }
  model CropPatch { x: float64; y: float64; width: float64; height: float64; }
  model WebcamState {
    supported: boolean; backend: "avfoundation" | "ffmpeg" | "unsupported";
    enabled: boolean; captureLocked: boolean;
    status: "off" | "warming" | "ready" | "recording" | "saving" | "error";
    error: string | null; devices: WebcamDevice[]; selectedDeviceId: string | null;
    quality: "compact" | "balanced" | "quality"; offsetMs: int32; recordingLimitMb: int32;
    retentionDays: int32; playbackVisible: boolean; liveVisible: boolean;
    mirror: boolean; flipVertical: boolean; overlayX: float64; overlayY: float64;
    overlayWidth: float64; crop: WebcamCrop;
  }
  model WebcamSettings {
    enabled?: boolean; deviceId?: string | null; quality?: "compact" | "balanced" | "quality";
    offsetMs?: int32; recordingLimitMb?: int32; retentionDays?: int32;
    playbackVisible?: boolean; liveVisible?: boolean; mirror?: boolean;
    flipVertical?: boolean; overlayX?: float64; overlayY?: float64;
    overlayWidth?: float64; crop?: CropPatch;
  }
  model WebcamPreview { width: int32; height: int32; file: ipc.File; }
  model FfmpegState {
    status: string; platform: string | null; source: string | null;
    installationDirectory: string | null; path: string | null; error: string | null;
    downloadedBytes: safeint; totalBytes: safeint | null; available: boolean; busy: boolean;
  }
  model RendererState {
    status: string; source: string | null; installationDirectory: string | null;
    version: string | null; error: string | null; errorCode: string | null;
    downloadedBytes: safeint; totalBytes: safeint | null;
  }
  model DownloadsState { renderer: RendererState; ffmpeg: FfmpegState; cameraNeedsFfmpeg: boolean; }
  model RenderErrorDetails { field: string | null; line: int32 | null; file: string | null; }
  model RenderBundleState {
    jobId: string; runId: string; state: "preparing" | "completed" | "failed" | "cancelled";
    progress: float64; manifestPath: string | null; errorCode: string | null;
    errorMessage: string | null; errorDetails: RenderErrorDetails | null;
  }

  op readHealth(): Health;
  op listAppSessions(offset?: int32, limit?: int32): AppSession[];
  op readLegacyReplayStatus(): LegacyReplayStatus;
  op readLevelSession(id: string): LevelSession;
  op listLevelSessionRuns(id: string, offset?: int32, limit?: int32): Run[];
  op readLevelSessionChart(id: string): Chart;
  op readLogicalLevel(id: string): LogicalLevel;
  op listLogicalLevelRuns(id: string, appSessionIds: string[], offset?: int32, limit?: int32): Run[];
  op readLogicalLevelChart(id: string): Chart;
  op removeRun(runId: string): DeleteResult;
  op removeMicrophoneRecording(runId: string): DeleteResult;
  op retainMicrophoneRecording(runId: string): RetainResult;
  op exportMicrophoneRecording(runId: string): ipc.File;
  op removeCameraRecording(runId: string): DeleteResult;
  op retainCameraRecording(runId: string): RetainResult;
  op exportCameraRecording(runId: string): ipc.File;
  op startReplay(runId: string, levelPath?: string | null): ReplayState;
  op readReplayState(): ReplayState;
  op chooseLevelFile(runId: string, purpose?: "replay" | "render"): LevelFileResult;
  op refreshMicrophoneDevices(): MicrophoneDevices;
  op changeMicrophoneAccess(enabled: boolean): MicrophoneDevices;
  op chooseMicrophoneDevice(deviceId: string | null): MicrophoneDevices;
  op changeMicrophoneOffset(offsetMs: int32): MicrophoneTiming;
  op changeMicrophoneVolume(volumeDb: int32): MicrophoneTiming;
  op startCalibration(): CalibrationState;
  op readCalibrationState(operationId: string): CalibrationState;
  op readCalibrationResult(operationId: string, revision?: int32): CalibrationResult;
  op startCalibrationPreview(operationId: string): CalibrationState;
  op stopCalibrationPreview(operationId: string): CalibrationState;
  op changeCalibrationOffset(operationId: string, offsetMs: int32): CalibrationState;
  op changeCalibrationVolume(operationId: string, volumeDb: int32): CalibrationState;
  op closeCalibration(operationId: string): CalibrationState;
  op refreshWebcamState(refreshDevices?: boolean): WebcamState;
  op changeWebcamSettings(settings: WebcamSettings): WebcamState;
  op readWebcamPreview(deviceId: string | null): WebcamPreview | null;
  op readFfmpegState(): FfmpegState;
  op requestFfmpeg(): FfmpegState;
  op confirmFfmpeg(): FfmpegState;
  op declineFfmpeg(): FfmpegState;
  op cancelFfmpeg(): FfmpegState;
  op releaseFfmpeg(): FfmpegState;
  op readDownloadsState(): DownloadsState;
  op requestFfmpegDownload(): DownloadsState;
  op confirmFfmpegDownload(): DownloadsState;
  op cancelFfmpegDownload(): DownloadsState;
  op requestRendererDownload(): DownloadsState;
  op confirmRendererDownload(): DownloadsState;
  op cancelRendererDownload(): DownloadsState;
  op prepareRenderBundle(runId: string, levelPath?: string | null, includeWebcam?: boolean, includeMicrophone?: boolean): RenderBundleState;
  op readRenderBundleState(jobId: string): RenderBundleState;
  op cancelRenderBundle(jobId: string): RenderBundleState;

  @ipc.app
  interface Companion {
    healthSnapshot(value: Health): void;
    activityChanged(value: ActivityChanged): void;
    replayStateChanged(value: ReplayState): void;
    microphoneDevicesChanged(value: MicrophoneDevices): void;
    microphoneTimingChanged(value: MicrophoneTiming): void;
    calibrationStateChanged(value: CalibrationState): void;
    webcamStateChanged(value: WebcamState): void;
    ffmpegStateChanged(value: FfmpegState): void;
    downloadsStateChanged(value: DownloadsState): void;
    renderBundleStateChanged(value: RenderBundleState): void;
  }
}
