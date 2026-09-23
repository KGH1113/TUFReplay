import type { z } from "zod";
import type {
  submissionPageSchema,
  submissionRunSchema,
  submissionStatusSchema,
  visualSelectionSchema,
} from "@/schemas/submission/submission-schema";

export type SubmissionStatus = z.infer<typeof submissionStatusSchema>;
export type SubmissionRun = z.infer<typeof submissionRunSchema>;
export type SubmissionPage = z.infer<typeof submissionPageSchema>;
export type VisualSelection = z.infer<typeof visualSelectionSchema>;

export function hasSubmissionPermission(
  status: SubmissionStatus | undefined,
  statusRequestFailed = false,
) {
  return (
    !statusRequestFailed &&
    status?.connected === true &&
    status.accountStatus === "available" &&
    status.canSubmit === true
  );
}

export function submissionAccountKey(status: SubmissionStatus | undefined) {
  if (status?.connected !== true) return null;
  const username = status.username?.trim();
  if (username) return `username:${username}`;
  const nickname = status.nickname?.trim();
  return nickname ? `nickname:${nickname}` : null;
}

/**
 * Keeps a gallery choice available until the server confirms that the run has
 * a fixed presentation. A failed request can therefore be retried with the
 * same body, while a run that was fixed by a response that the client lost
 * retries without sending a replacement presentation.
 */
export function submissionPresentationForRequest(
  serverPresentation: VisualSelection | null | undefined,
  selectedPresentation: VisualSelection | undefined,
  attemptedPresentation: VisualSelection | null,
) {
  if (serverPresentation != null) return undefined;
  return selectedPresentation ?? attemptedPresentation ?? undefined;
}

export function canSubmit(run: SubmissionRun) {
  if (run.status === "registration_error") return true;
  if (run.evidence_expires_at && Date.parse(run.evidence_expires_at) <= Date.now()) return false;
  return ["evidence_ready", "validation_error", "validator_unavailable"].includes(run.status);
}
export function canDeleteSubmission(run: SubmissionRun) {
  return [
    "evidence_ready",
    "validation_error",
    "validator_unavailable",
    "validation_rejected",
    "registration_rejected",
    "evidence_invalid",
  ].includes(run.status);
}
export function submissionPhase(status: string) {
  if (["issued", "streaming", "uploading", "sealed"].includes(status)) return "uploading";
  if (status === "evidence_ready") return "ready";
  if (["validation_pending", "registering"].includes(status)) return "submitting";
  if (status === "submitted") return "submitted";
  if (status === "validator_unavailable") return "unavailable";
  if (["validation_error", "registration_error"].includes(status)) return "retry";
  return "ineligible";
}
