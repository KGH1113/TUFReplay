export interface RunMutationResult {
  runId: string;
  changed: boolean;
}

export interface RunApi {
  prepareMicrophoneRecordingDownload(runId: string): Promise<RecordingDownload>;
  prepareCameraRecordingDownload(runId: string): Promise<RecordingDownload>;
  deleteRun(runId: string): Promise<RunMutationResult>;
  deleteMicrophoneRecording(runId: string): Promise<RunMutationResult>;
  keepMicrophoneRecording(runId: string): Promise<RunMutationResult>;
  deleteCameraRecording(runId: string): Promise<RunMutationResult>;
  keepCameraRecording(runId: string): Promise<RunMutationResult>;
}

import type { RecordingDownload } from "@/ports/received-file";
