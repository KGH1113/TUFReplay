import { callRecorder } from "@/api/recorder-call";
import type { RunJudgmentTimeline } from "@/models/activity/judgment-timeline";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import { createBatchLoader } from "@/shared/lib/batch-loader";
import type { RunApi } from "./run-api";

// The mod waits until the player leaves an attempt before a bulk delete, so allow longer than a call.
const DAY_DELETE_TIMEOUT_MS = 180_000;
// Judgment timelines also wait for the attempt to end before decoding payloads.
const TIMELINE_TIMEOUT_MS = 180_000;

export function createRunApi(clients: LocalAppChannels): RunApi {
  // Visible rows ask together; the mod reads them in one pass once the player is between attempts.
  const timelines = createBatchLoader<RunJudgmentTimeline>(async (runIds) => {
    const results = await callRecorder(clients, (mod) => mod.readRunJudgmentTimelines({ runIds }), {
      timeoutMs: TIMELINE_TIMEOUT_MS,
    });
    return new Map(results.map((timeline) => [timeline.runId, timeline]));
  });
  return {
    loadJudgmentTimeline: (runId) => timelines.load(runId),
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
