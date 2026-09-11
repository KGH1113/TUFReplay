import type { ReplayLevelFilePickerResult, ReplayStatus } from "@/models/replay/replay-model";

export interface ReplayApi {
  play(runId: string, levelPath?: string): Promise<ReplayStatus>;
  getStatus(): Promise<ReplayStatus>;
  pickLevelFile(runId: string): Promise<ReplayLevelFilePickerResult>;
}
