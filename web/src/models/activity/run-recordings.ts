import type { ActivityRun } from "@/models/activity/activity-model";

export type RecordingKind = "microphone" | "camera";
export type RecordingAction = "download" | "keep" | "delete";
export type RunRecordingAction = (run: ActivityRun) => Promise<void>;
export type RunRecordingActions = Record<
  RecordingKind,
  Record<RecordingAction, RunRecordingAction>
>;

export function runRecording(run: ActivityRun, kind: RecordingKind) {
  return kind === "microphone"
    ? {
        available: run.hasMicrophoneRecording,
        bytes: run.microphoneRecordingBytes,
        permanent: run.microphoneRecordingPermanent,
        expiresAtUtc: run.microphoneRecordingExpiresAtUtc,
      }
    : {
        available: run.hasCameraRecording,
        bytes: run.cameraRecordingBytes,
        permanent: run.cameraRecordingPermanent,
        expiresAtUtc: run.cameraRecordingExpiresAtUtc,
      };
}

export function recordingTimeRemaining(expiresAtUtc: string | null, now = Date.now()) {
  if (!expiresAtUtc) return { unit: "soon" as const, count: 0 };
  const expiresAt = Date.parse(expiresAtUtc);
  if (!Number.isFinite(expiresAt) || expiresAt <= now) return { unit: "soon" as const, count: 0 };
  const hours = Math.ceil((expiresAt - now) / 3_600_000);
  return hours < 24
    ? { unit: "hours" as const, count: hours }
    : { unit: "days" as const, count: Math.ceil(hours / 24) };
}
