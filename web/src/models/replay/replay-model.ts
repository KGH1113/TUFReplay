import type {
  ReplayLevelFilePickerResultDto,
  ReplayStatusDto,
} from "@/schemas/replay/replay-schema";

export type ReplayState = ReplayStatusDto["State"];

export interface ReplayStatus {
  operationId: string | null;
  runId: string | null;
  state: ReplayState;
  errorCode: string | null;
  message: string | null;
}

export type ReplayLevelFilePickerOutcome = ReplayLevelFilePickerResultDto["Outcome"];

export interface ReplayLevelFilePickerResult {
  operationId: string | null;
  runId: string;
  outcome: ReplayLevelFilePickerOutcome;
  levelPath: string | null;
  errorCode: string | null;
  message: string | null;
}

export function mapReplayStatus(dto: ReplayStatusDto): ReplayStatus {
  return {
    operationId: dto.OperationId,
    runId: dto.RunId,
    state: dto.State,
    errorCode: dto.ErrorCode,
    message: dto.Message,
  };
}

export function mapReplayLevelFilePickerResult(
  dto: ReplayLevelFilePickerResultDto,
): ReplayLevelFilePickerResult {
  return {
    operationId: dto.OperationId,
    runId: dto.RunId,
    outcome: dto.Outcome,
    levelPath: dto.LevelPath,
    errorCode: dto.ErrorCode,
    message: dto.Message,
  };
}
