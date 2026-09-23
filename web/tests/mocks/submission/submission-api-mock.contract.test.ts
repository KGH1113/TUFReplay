import { describe, expect, it } from "bun:test";
import { createSubmissionApiMock } from "@/mocks/submission/submission-api-mock";
import { submissionPresentationForRequest } from "@/models/submission/submission-model";

const runId = "68727984-2424-4a6d-a72b-919044143454";
const firstChoice = { keyviewer_id: "jipper-1", overlay_id: null };

describe("submission gallery flow mock", () => {
  it("sends the selected gallery presentation once and omits it after fixation", async () => {
    const api = createSubmissionApiMock();
    await api.connect();

    const initial = await api.get(runId);
    const firstRequest = submissionPresentationForRequest(initial.presentation, firstChoice, null);
    expect(firstRequest).toEqual(firstChoice);
    const fixed = await api.submit(runId, firstRequest);
    expect(fixed.presentation).toEqual(firstChoice);

    const retryRequest = submissionPresentationForRequest(
      fixed.presentation,
      undefined,
      firstChoice,
    );
    expect(retryRequest).toBeUndefined();
    await expect(api.submit(runId, retryRequest)).resolves.toMatchObject({
      presentation: firstChoice,
    });
  });

  it("keeps the same choice through a pre-request failure but omits it after a lost response", async () => {
    const preRequestApi = createSubmissionApiMock();
    await preRequestApi.connect();
    const initial = await preRequestApi.get(runId);
    const firstRequest = submissionPresentationForRequest(initial.presentation, firstChoice, null);

    await expect(
      (async () => {
        throw new Error("status_refresh_failed");
      })(),
    ).rejects.toThrow("status_refresh_failed");
    expect(submissionPresentationForRequest(null, undefined, firstRequest ?? null)).toEqual(
      firstChoice,
    );
    await expect(preRequestApi.submit(runId, firstRequest)).resolves.toMatchObject({
      presentation: firstChoice,
    });

    const lostResponseApi = createSubmissionApiMock();
    await lostResponseApi.connect();
    const lostInitial = await lostResponseApi.get(runId);
    const lostRequest = submissionPresentationForRequest(
      lostInitial.presentation,
      firstChoice,
      null,
    );
    await expect(
      (async () => {
        await lostResponseApi.submit(runId, lostRequest);
        throw new Error("response_lost_after_commit");
      })(),
    ).rejects.toThrow("response_lost_after_commit");

    const refreshed = await lostResponseApi.get(runId);
    expect(
      submissionPresentationForRequest(refreshed.presentation, undefined, firstChoice),
    ).toBeUndefined();
    await expect(lostResponseApi.submit(runId, undefined)).resolves.toMatchObject({
      presentation: firstChoice,
    });
  });
});
