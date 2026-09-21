import { useCallback } from "react";

import { useApiPromise } from "@/api/app-api-provider";

export function useRunActions() {
  const apiPromise = useApiPromise();
  return {
    prepareMicrophoneRecordingDownload: useCallback(
      async (runId: string) => (await apiPromise).run.prepareMicrophoneRecordingDownload(runId),
      [apiPromise],
    ),
    deleteRun: useCallback(
      async (runId: string) => (await apiPromise).run.deleteRun(runId),
      [apiPromise],
    ),
    deleteMicrophoneRecording: useCallback(
      async (runId: string) => (await apiPromise).run.deleteMicrophoneRecording(runId),
      [apiPromise],
    ),
    keepMicrophoneRecording: useCallback(
      async (runId: string) => (await apiPromise).run.keepMicrophoneRecording(runId),
      [apiPromise],
    ),
  };
}
