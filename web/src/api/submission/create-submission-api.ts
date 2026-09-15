import { z } from "zod";
import { createHealthApi } from "@/api/health/create-health-api";
import type { SubmissionApi } from "@/api/submission/submission-api";
import { getAutoSubmissionCompatibility } from "@/models/health/health-model";
import {
  submissionPageSchema,
  submissionRunSchema,
  submissionStatusSchema,
} from "@/schemas/submission/submission-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";
import { prepareOAuthWindow, takeOAuthCallback } from "@/shared/clients/oauth-browser";
import {
  TUFREPLAY_WEB_BUILD,
  type TufReplayBuildFlavor,
} from "@/shared/config/tufreplay-build-info";
import { ApiError } from "@/shared/errors/api-error";

export function createSubmissionApi(
  clients: AdofaiIpcClients,
  browser = { prepareOAuthWindow, takeOAuthCallback },
  webFlavor: TufReplayBuildFlavor = TUFREPLAY_WEB_BUILD.flavor,
): SubmissionApi {
  let callback = browser.takeOAuthCallback();
  let completion: Promise<unknown> | null = null;
  const ipc = (method: string, params = {}) =>
    callAdofaiIpc(clients.namespace, method, params, submissionStatusSchema);
  const healthApi = createHealthApi(clients);
  async function requireCompatibleBuild() {
    const health = webFlavor === "auto-submission" ? await healthApi.get() : null;
    if (!getAutoSubmissionCompatibility(webFlavor, health).available) {
      throw new ApiError("Auto-submission requires a compatible test build.", {
        kind: "protocol",
        code: "auto_submission_build_mismatch",
      });
    }
  }
  async function completeCallback() {
    if (!callback) return;
    completion ??= callAdofaiIpc(
      clients.namespace,
      "submission.oauth.complete",
      callback,
      z.object({ completed: z.literal(true) }),
    );
    try {
      await completion;
      callback = null;
    } finally {
      completion = null;
    }
  }
  return {
    async connect() {
      callback = null;
      const navigate = browser.prepareOAuthWindow();
      try {
        await requireCompatibleBuild();
        const result = await callAdofaiIpc(
          clients.namespace,
          "submission.oauth.begin",
          {},
          z.object({ authorizationUrl: z.url() }),
        );
        navigate(result.authorizationUrl);
        return submissionStatusSchema.parse({
          connected: false,
          configured: true,
          disabled: false,
          state: "authorizing",
        });
      } catch (error) {
        navigate(null);
        throw error;
      }
    },
    async disconnect() {
      await requireCompatibleBuild();
      await callAdofaiIpc(
        clients.namespace,
        "submission.account.disconnect",
        {},
        z.object({ disconnected: z.literal(true) }),
      );
      return submissionStatusSchema.parse({
        connected: false,
        configured: true,
        disabled: false,
        state: "disconnected",
      });
    },
    async status() {
      await requireCompatibleBuild();
      await completeCallback();
      return ipc("submission.status.get");
    },
    async setDisabled(disabled) {
      await requireCompatibleBuild();
      return ipc("submission.disabled.set", { disabled });
    },
    async list(before) {
      await requireCompatibleBuild();
      return callAdofaiIpc(
        clients.namespace,
        "submission.runs.list",
        before === undefined ? {} : { before },
        submissionPageSchema,
      );
    },
    async get(runId) {
      await requireCompatibleBuild();
      return callAdofaiIpc(clients.namespace, "submission.run.get", { runId }, submissionRunSchema);
    },
    async submit(runId) {
      await requireCompatibleBuild();
      return callAdofaiIpc(
        clients.namespace,
        "submission.run.submit",
        { runId },
        submissionRunSchema,
      );
    },
    async remove(runId) {
      await requireCompatibleBuild();
      await callAdofaiIpc(
        clients.namespace,
        "submission.run.remove",
        { runId },
        z.object({ deleted: z.literal(true) }),
      );
    },
  };
}
