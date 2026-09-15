import { describe, expect, it } from "bun:test";
import { hasSubmissionPermission } from "@/models/submission/submission-model";
import { submissionStatusSchema } from "@/schemas/submission/submission-schema";

const availableStatus = submissionStatusSchema.parse({
  connected: true,
  configured: true,
  disabled: false,
  state: "ready",
  username: "impl.dev",
  nickname: "impl",
  accountStatus: "available",
  canSubmit: true,
  denialReason: null,
});

describe("submission permission", () => {
  it("requires a connected account with a current available capability", () => {
    expect(hasSubmissionPermission(availableStatus)).toBe(true);

    for (const accountStatus of ["checking", "stale", "unavailable"] as const) {
      expect(
        hasSubmissionPermission({
          ...availableStatus,
          accountStatus,
          canSubmit: true,
        }),
      ).toBe(false);
    }
  });

  it("fails closed when eligibility refresh fails or the server denies access", () => {
    expect(hasSubmissionPermission(availableStatus, true)).toBe(false);
    expect(hasSubmissionPermission(undefined)).toBe(false);
    expect(
      hasSubmissionPermission({
        ...availableStatus,
        canSubmit: false,
        denialReason: "auto_submission_tester_required",
      }),
    ).toBe(false);
  });

  it("keeps submission eligibility independent from the new-capture preference", () => {
    expect(hasSubmissionPermission({ ...availableStatus, disabled: true })).toBe(true);
  });
});
