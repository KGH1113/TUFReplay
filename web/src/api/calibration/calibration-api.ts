import type { CalibrationResult, CalibrationStatus } from "@/models/calibration/calibration-model";

export interface CalibrationApi {
  start(): Promise<CalibrationStatus>;
  getStatus(operationId: string): Promise<CalibrationStatus>;
  getResult(operationId: string, revision: number): Promise<CalibrationResult>;
  playPreview(operationId: string): Promise<CalibrationStatus>;
  stopPreview(operationId: string): Promise<CalibrationStatus>;
  setOffset(operationId: string, offsetMs: number): Promise<CalibrationStatus>;
  setVolume(operationId: string, volumeDb: number): Promise<CalibrationStatus>;
  close(operationId: string): Promise<CalibrationStatus>;
}
