import { canSubmit, type SubmissionRun } from "@/models/submission/submission-model";

/** Keep pending/retryable submissions reachable, but hide terminally unavailable actions. */
export function canContinueSubmission(run: SubmissionRun | undefined) {
  if (!run) return true;
  const progress = submissionProgress(run);
  return progress.phase !== "submitted" && (progress.processing || canSubmit(run));
}

export const submissionSteps = [
  "uploading",
  "saving",
  "ready",
  "validating",
  "registering",
] as const;
export type SubmissionProgressPhase =
  | (typeof submissionSteps)[number]
  | "submitted"
  | "unavailable"
  | "validationError"
  | "registrationError"
  | "validationRejected"
  | "registrationRejected"
  | "invalid"
  | "expired"
  | "unknown";

export function isSubmissionProcessing(status: string | undefined) {
  return [
    "issued",
    "streaming",
    "uploading",
    "sealed",
    "validation_pending",
    "registering",
  ].includes(status ?? "");
}

export function submissionProgress(run: SubmissionRun | undefined, now = Date.now()) {
  let phase: SubmissionProgressPhase = "unknown";
  let step = -1;
  let failed = false;
  const status = run?.status;
  if (status === "submitted" || run?.external_pass_id != null) {
    phase = "submitted";
    step = 5;
  } else if (["issued", "streaming", "uploading"].includes(status ?? "")) {
    phase = "uploading";
    step = 0;
  } else if (status === "sealed") {
    phase = "saving";
    step = 1;
  } else if (
    status === "expired" ||
    (run?.evidence_expires_at &&
      ["evidence_ready", "validation_error", "validator_unavailable"].includes(status ?? "") &&
      Date.parse(run.evidence_expires_at) <= now)
  ) {
    phase = "expired";
  } else if (status === "evidence_ready") {
    phase = "ready";
    step = 2;
  } else if (status === "validation_pending") {
    phase = "validating";
    step = 3;
  } else if (status === "registering") {
    phase = "registering";
    step = 4;
  } else if (status === "validator_unavailable") {
    phase = "unavailable";
    step = 3;
  } else if (status === "validation_error" || status === "validation_rejected") {
    phase = status === "validation_error" ? "validationError" : "validationRejected";
    step = 3;
    failed = true;
  } else if (status === "registration_error" || status === "registration_rejected") {
    phase = status === "registration_error" ? "registrationError" : "registrationRejected";
    step = 4;
    failed = true;
  } else if (status === "evidence_invalid") {
    phase = "invalid";
    step = 1;
    failed = true;
  }
  return {
    phase,
    step,
    failed,
    processing: isSubmissionProcessing(status) && phase !== "submitted",
  };
}

/** Only known server reasons become user-facing copy; never expose internal error text. */
export function submissionReason(reason: string | null | undefined) {
  switch (reason) {
    case "submission_chart_gameplay_mismatch":
      return "chartMismatch";
    case "submission_chart_identity_missing_or_unsupported":
    case "submission_chart_identity_invalid":
      return "chartIdentity";
    case "official_chart_ambiguous":
      return "chartAmbiguous";
    case "official_chart_unsupported":
      return "chartUnsupported";
    case "level_revision_changed":
      return "chartChanged";
    case "submission_authorization_required":
      return "authorization";
    default:
      return null;
  }
}
