import { useCallback } from "react";

import { useApiPromise } from "@/api/app-api-provider";

export function useRunActions() {
  const apiPromise = useApiPromise();
  return {
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
