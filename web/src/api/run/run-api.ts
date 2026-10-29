export interface RunMutationResult {
  runId: string;
  changed: boolean;
}

export interface RunLockResult {
  runId: string;
  locked: boolean;
}

export interface RunBatchDeleteResult {
  deletedRunCount: number;
  keptLockedRunCount: number;
  keptInUseRunCount: number;
}

export interface RunApi {
  prepareMicrophoneRecordingDownload(runId: string): Promise<RecordingDownload>;
  prepareCameraRecordingDownload(runId: string): Promise<RecordingDownload>;
  deleteRun(runId: string): Promise<RunMutationResult>;
  deleteMicrophoneRecording(runId: string): Promise<RunMutationResult>;
  keepMicrophoneRecording(runId: string): Promise<RunMutationResult>;
  deleteCameraRecording(runId: string): Promise<RunMutationResult>;
  keepCameraRecording(runId: string): Promise<RunMutationResult>;
  setRunLocked(runId: string, locked: boolean): Promise<RunLockResult>;
  /** A run's non-perfect judgments over time; null when the run has no readable judgment data. */
  loadJudgmentTimeline(runId: string): Promise<RunJudgmentTimeline | null>;
  /** Deletes the unlocked runs of a day's app sessions. Waits while the player is mid-attempt. */
  deleteAppSessionRuns(appSessionIds: string[], keepClears: boolean): Promise<RunBatchDeleteResult>;
}

import type { RunJudgmentTimeline } from "@/models/activity/judgment-timeline";
import type { RecordingDownload } from "@/ports/received-file";
