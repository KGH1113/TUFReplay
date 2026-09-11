import type { z } from "zod";
import type {
  submissionPageSchema,
  submissionRunSchema,
  submissionStatusSchema,
} from "@/schemas/submission/submission-schema";

export type SubmissionStatus = z.infer<typeof submissionStatusSchema>;
export type SubmissionRun = z.infer<typeof submissionRunSchema>;
export type SubmissionPage = z.infer<typeof submissionPageSchema>;

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
