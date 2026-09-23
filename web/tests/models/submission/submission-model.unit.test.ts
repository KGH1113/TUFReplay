import { describe, expect, it } from "bun:test";
import {
  hasSubmissionPermission,
  submissionAccountKey,
  submissionPresentationForRequest,
} from "@/models/submission/submission-model";
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

describe("submission presentation request", () => {
  const firstChoice = { keyviewer_id: "jipper-1", overlay_id: null };
  const correctedChoice = { keyviewer_id: null, overlay_id: "overlay-1" };

  it("opens with the gallery choice and resends it after a pre-request failure", () => {
    expect(submissionPresentationForRequest(null, firstChoice, null)).toEqual(firstChoice);
    expect(submissionPresentationForRequest(null, undefined, firstChoice)).toEqual(firstChoice);
  });

  it("allows a validation retry to replace an unfixed choice", () => {
    expect(submissionPresentationForRequest(null, correctedChoice, firstChoice)).toEqual(
      correctedChoice,
    );
  });

  it("omits the presentation once a lost response has fixed the run", () => {
    expect(
      submissionPresentationForRequest(firstChoice, correctedChoice, firstChoice),
    ).toBeUndefined();
  });
});

describe("submission account scope", () => {
  it("uses the connected account identity and clears it when disconnected", () => {
    expect(submissionAccountKey(availableStatus)).toBe("username:impl.dev");
    expect(submissionAccountKey({ ...availableStatus, username: null, nickname: "impl" })).toBe(
      "nickname:impl",
    );
    expect(submissionAccountKey({ ...availableStatus, connected: false })).toBeNull();
  });
});
