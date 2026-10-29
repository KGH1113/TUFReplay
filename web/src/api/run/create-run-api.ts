import { callRecorder } from "@/api/recorder-call";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import type { RunApi } from "./run-api";

// The mod waits until the player leaves an attempt before a bulk delete, so allow longer than a call.
const DAY_DELETE_TIMEOUT_MS = 180_000;

export function createRunApi(clients: LocalAppChannels): RunApi {
  return {
    prepareMicrophoneRecordingDownload: (runId) =>
      callRecorder(clients, (mod) => mod.exportMicrophoneRecording({ runId })),
    prepareCameraRecordingDownload: (runId) =>
      callRecorder(clients, (mod) => mod.exportCameraRecording({ runId })),
    async deleteRun(runId) {
      const result = await callRecorder(clients, (mod) => mod.removeRun({ runId }));
      return { runId: result.runId, changed: result.deleted };
    },
    async deleteMicrophoneRecording(runId) {
      const result = await callRecorder(clients, (mod) => mod.removeMicrophoneRecording({ runId }));
      return { runId: result.runId, changed: result.deleted };
    },
    async keepMicrophoneRecording(runId) {
      const result = await callRecorder(clients, (mod) => mod.retainMicrophoneRecording({ runId }));
      return { runId: result.runId, changed: result.permanent };
    },
    async deleteCameraRecording(runId) {
      const result = await callRecorder(clients, (mod) => mod.removeCameraRecording({ runId }));
      return { runId: result.runId, changed: result.deleted };
    },
    async keepCameraRecording(runId) {
      const result = await callRecorder(clients, (mod) => mod.retainCameraRecording({ runId }));
      return { runId: result.runId, changed: result.permanent };
    },
    async setRunLocked(runId, locked) {
      const result = await callRecorder(clients, (mod) => mod.changeRunLock({ runId, locked }));
      return { runId: result.runId, locked: result.locked };
    },
    async deleteAppSessionRuns(appSessionIds, keepClears) {
      const result = await callRecorder(
        clients,
        (mod) => mod.removeAppSessionRuns({ appSessionIds, keepClears }),
        { timeoutMs: DAY_DELETE_TIMEOUT_MS },
      );
      return {
        deletedRunCount: result.deletedRunCount,
        keptLockedRunCount: result.keptLockedRunCount,
        keptInUseRunCount: result.keptInUseRunCount,
      };
    },
  };
}
