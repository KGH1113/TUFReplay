export interface RunMutationResult {
  runId: string;
  changed: boolean;
}

export interface RunApi {
  deleteRun(runId: string): Promise<RunMutationResult>;
  deleteMicrophoneRecording(runId: string): Promise<RunMutationResult>;
  keepMicrophoneRecording(runId: string): Promise<RunMutationResult>;
}
