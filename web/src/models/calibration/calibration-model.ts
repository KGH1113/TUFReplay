import type {
  CalibrationResultDto,
  CalibrationStatusDto,
} from "@/schemas/calibration/calibration-schema";

export type CalibrationState = CalibrationStatusDto["State"];
export type CalibrationStatus = ReturnType<typeof mapCalibrationStatus>;
export type CalibrationResult = ReturnType<typeof mapCalibrationResult>;

export function mapCalibrationStatus(dto: CalibrationStatusDto) {
  return {
    operationId: dto.OperationId,
    state: dto.State,
    errorCode: dto.ErrorCode,
    message: dto.Message,
    durationMs: dto.DurationMs,
    playbackPositionMs: dto.PlaybackPositionMs,
    resultRevision: dto.ResultRevision,
    microphoneOffsetMs: dto.MicrophoneOffsetMs,
    microphoneVolumeDb: dto.MicrophoneVolumeDb,
  };
}

export function mapCalibrationResult(dto: CalibrationResultDto) {
  return {
    operationId: dto.OperationId,
    revision: dto.Revision,
    durationMs: dto.DurationMs,
    gameWaveform: dto.GameWaveform,
    songWaveform: dto.SongWaveform,
    microphoneWaveform: dto.MicrophoneWaveform,
  };
}
