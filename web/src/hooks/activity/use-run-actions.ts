import { useCallback } from "react";

import { useApiPromise } from "@/api/app-api-provider";

export function useRunActions() {
  const apiPromise = useApiPromise();
  return {
    prepareCameraRecordingDownload: useCallback(
      async (runId: string) => (await apiPromise).run.prepareCameraRecordingDownload(runId),
      [apiPromise],
    ),
    deleteCameraRecording: useCallback(
      async (runId: string) => (await apiPromise).run.deleteCameraRecording(runId),
      [apiPromise],
    ),
    keepCameraRecording: useCallback(
      async (runId: string) => (await apiPromise).run.keepCameraRecording(runId),
      [apiPromise],
    ),
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
    setRunLocked: useCallback(
      async (runId: string, locked: boolean) => (await apiPromise).run.setRunLocked(runId, locked),
      [apiPromise],
    ),
    deleteAppSessionRuns: useCallback(
      async (appSessionIds: string[], keepClears: boolean) =>
        (await apiPromise).run.deleteAppSessionRuns(appSessionIds, keepClears),
      [apiPromise],
    ),
  };
}
