import { describe, expect, it } from "bun:test";
import type { SubmissionRun } from "@/models/submission/submission-model";
import {
  canContinueSubmission,
  isSubmissionProcessing,
  submissionProgress,
  submissionReason,
} from "@/models/submission/submission-progress";

const recording: SubmissionRun = {
  cursor: 1,
  run_id: "68727984-2424-4a6d-a72b-919044143454",
  tuf_level_id: 8068,
  chart_path: "main.adofai",
  status: "sealed",
  reason: null,
  external_pass_id: null,
  created_at: "2026-09-25T00:00:00Z",
  evidence_expires_at: null,
  presentation: null,
};

describe("submission progress", () => {
  it("keeps loading, active and retryable submissions accessible without offering terminal runs", () => {
    expect(canContinueSubmission(undefined)).toBe(true);
    for (const status of [
      "issued",
      "streaming",
      "sealed",
      "evidence_ready",
      "validation_pending",
      "registering",
      "validation_error",
      "registration_error",
      "validator_unavailable",
    ]) {
      expect(canContinueSubmission({ ...recording, status })).toBe(true);
    }
    for (const status of [
      "validation_rejected",
      "registration_rejected",
      "evidence_invalid",
      "expired",
      "submitted",
      "future_state",
    ]) {
      expect(canContinueSubmission({ ...recording, status })).toBe(false);
    }
    expect(
      canContinueSubmission({
        ...recording,
        status: "evidence_ready",
        evidence_expires_at: "2000-01-01T00:00:00Z",
      }),
    ).toBe(false);
    expect(canContinueSubmission({ ...recording, external_pass_id: 123 })).toBe(false);
  });

  it("marks transfer complete when sealed, but does not claim storage is complete", () => {
    expect(submissionProgress(recording)).toEqual({
      phase: "saving",
      step: 1,
      failed: false,
      processing: true,
    });
    for (const status of ["issued", "streaming", "uploading"]) {
      expect(submissionProgress({ ...recording, status }).step).toBe(0);
    }
  });

  it("waits for user submission after evidence becomes ready", () => {
    expect(submissionProgress({ ...recording, status: "evidence_ready" })).toEqual({
      phase: "ready",
      step: 2,
      failed: false,
      processing: false,
    });
    expect(isSubmissionProcessing("evidence_ready")).toBe(false);
    expect(submissionProgress({ ...recording, status: "validation_pending" }).phase).toBe(
      "validating",
    );
    expect(submissionProgress({ ...recording, status: "registering" }).phase).toBe("registering");
  });

  it("preserves which server step failed and does not mark later steps complete", () => {
    for (const status of ["validation_error", "validation_rejected"]) {
      expect(submissionProgress({ ...recording, status })).toMatchObject({
        step: 3,
        failed: true,
        processing: false,
      });
    }
    for (const status of ["registration_error", "registration_rejected"]) {
      expect(submissionProgress({ ...recording, status })).toMatchObject({
        step: 4,
        failed: true,
        processing: false,
      });
    }
    expect(submissionProgress({ ...recording, status: "validator_unavailable" })).toMatchObject({
      phase: "unavailable",
      failed: false,
      processing: false,
    });
  });

  it("never restarts progress after a confirmed registration, including a lost response", () => {
    const result = submissionProgress({ ...recording, external_pass_id: 123 });
    expect(result).toMatchObject({ phase: "submitted", step: 5, processing: false });
  });

  it("expires retryable evidence without expiring an in-flight or completed registration", () => {
    const expired = { ...recording, evidence_expires_at: "2026-09-25T00:00:00Z" };
    const now = Date.parse("2026-09-25T01:00:00Z");
    for (const status of ["evidence_ready", "validation_error", "validator_unavailable"]) {
      expect(submissionProgress({ ...expired, status }, now).phase).toBe("expired");
    }
    for (const status of ["validation_pending", "registering", "registration_error", "submitted"]) {
      expect(submissionProgress({ ...expired, status }, now).phase).not.toBe("expired");
    }
  });

  it("does not invent successful steps for missing or unknown server states", () => {
    expect(submissionProgress(undefined).step).toBe(-1);
    expect(submissionProgress({ ...recording, status: "future_state" })).toMatchObject({
      phase: "unknown",
      step: -1,
      processing: false,
    });
    expect(submissionReason("internal_error_with_sensitive_details")).toBeNull();
    expect(submissionReason("submission_chart_gameplay_mismatch")).toBe("chartMismatch");
  });
});
