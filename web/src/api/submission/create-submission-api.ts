import { z } from "zod";
import type { SubmissionApi } from "@/api/submission/submission-api";
import {
  submissionPageSchema,
  submissionRunSchema,
  submissionStatusSchema,
} from "@/schemas/submission/submission-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";
import { prepareOAuthWindow, takeOAuthCallback } from "@/shared/clients/oauth-browser";

export function createSubmissionApi(
  clients: AdofaiIpcClients,
  browser = { prepareOAuthWindow, takeOAuthCallback },
): SubmissionApi {
  let callback = browser.takeOAuthCallback();
  let completion: Promise<unknown> | null = null;
  const ipc = (method: string, params = {}) =>
    callAdofaiIpc(clients.namespace, method, params, submissionStatusSchema);
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
        const result = await callAdofaiIpc(
          clients.namespace,
          "submission.oauth.begin",
          {},
          z.object({ authorizationUrl: z.url() }),
        );
        navigate(result.authorizationUrl);
        return { connected: false, configured: true, disabled: false, state: "authorizing" };
      } catch (error) {
        navigate(null);
        throw error;
      }
    },
    async disconnect() {
      await callAdofaiIpc(
        clients.namespace,
        "submission.account.disconnect",
        {},
        z.object({ disconnected: z.literal(true) }),
      );
      return { connected: false, configured: true, disabled: false, state: "disconnected" };
    },
    async status() {
      await completeCallback();
      return ipc("submission.status.get");
    },
    setDisabled(disabled) {
      return ipc("submission.disabled.set", { disabled });
    },
    list(before) {
      return callAdofaiIpc(
        clients.namespace,
        "submission.runs.list",
        before === undefined ? {} : { before },
        submissionPageSchema,
      );
    },
    get(runId) {
      return callAdofaiIpc(clients.namespace, "submission.run.get", { runId }, submissionRunSchema);
    },
    submit(runId) {
      return callAdofaiIpc(
        clients.namespace,
        "submission.run.submit",
        { runId },
        submissionRunSchema,
      );
    },
    async remove(runId) {
      await callAdofaiIpc(
        clients.namespace,
        "submission.run.remove",
        { runId },
        z.object({ deleted: z.literal(true) }),
      );
    },
  };
}
