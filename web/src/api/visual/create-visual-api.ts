import type { z } from "zod";
import type { VisualApi, VisualPresetImport } from "@/api/visual/visual-api";
import { visualSourceUsesPresetFile } from "@/models/visual/visual-model";
import {
  visualInspectionSchema,
  visualPresetPageSchema,
  visualPresetResponseSchema,
  visualRegistrationSchema,
  visualRegistrationStartSchema,
  visualRemoveResponseSchema,
  visualSourcesSchema,
} from "@/schemas/visual/visual-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";
import { ApiError } from "@/shared/errors/api-error";

export function createVisualApi(clients: AdofaiIpcClients): VisualApi {
  return {
    async listPresets() {
      return callAdofaiIpc(clients.namespace, "visual.presets.list", {}, visualPresetPageSchema);
    },
    async getSources() {
      return callAdofaiIpc(clients.namespace, "visual.sources.get", {}, visualSourcesSchema);
    },
    async importPreset(input: VisualPresetImport) {
      const params = importParams(input);
      const response = await callAdofaiIpc(
        clients.namespace,
        "visual.presets.import",
        params,
        visualPresetResponseSchema,
      );
      return response.preset;
    },
    async inspectPreset(input) {
      return callAdofaiIpc(
        clients.namespace,
        "visual.presets.inspect",
        importParams(input),
        visualInspectionSchema,
      );
    },
    async registerPreset(input, onProgress) {
      const operationId = crypto.randomUUID();
      try {
        await callAdofaiIpc(
          clients.namespace,
          "visual.presets.registration.start",
          { ...importParams(input), operationId },
          visualRegistrationStartSchema,
        );
      } catch (error) {
        // A lost start response may already have launched an upload. Query its ID, never restart it.
        if (!(error instanceof ApiError) || error.kind !== "connection") throw error;
      }
      let failures = 0;
      const deadline = Date.now() + 10 * 60 * 1000;
      for (;;) {
        if (Date.now() > deadline)
          throw new ApiError("Registration status is unavailable.", {
            kind: "domain",
            code: "visual_registration_expired",
          });
        let status: z.infer<typeof visualRegistrationSchema> | undefined;
        try {
          status = await callAdofaiIpc(
            clients.namespace,
            "visual.presets.registration.get",
            { operationId },
            visualRegistrationSchema,
          );
          failures = 0;
        } catch (error) {
          if (!(error instanceof ApiError) || error.kind !== "connection") throw error;
          if (++failures >= 3)
            throw new ApiError("Registration status is unavailable.", {
              kind: "domain",
              code: "visual_registration_expired",
              cause: error,
            });
        }
        if (status) {
          onProgress?.(status.progress);
          if (status.state === "failed")
            throw new ApiError("Preset registration failed.", {
              kind: "domain",
              code: status.error.code,
            });
          if (status.state !== "running") return status;
        }
        await new Promise((resolve) => setTimeout(resolve, 400));
      }
    },
    async removePreset(id: string) {
      await callAdofaiIpc(
        clients.namespace,
        "visual.presets.remove",
        { id },
        visualRemoveResponseSchema,
      );
    },
    async renamePreset(id: string, name: string) {
      const response = await callAdofaiIpc(
        clients.namespace,
        "visual.presets.rename",
        { id, name: name.trim() },
        visualPresetResponseSchema,
      );
      return response.preset;
    },
  };
}

function importParams(input: VisualPresetImport): Record<string, string> {
  const params: Record<string, string> = {
    name: input.name.trim(),
    kind: input.kind,
    source: input.source,
  };
  if (visualSourceUsesPresetFile(input.source) && input.presetJson !== undefined)
    params.presetJson = input.presetJson;
  if (input.assets?.length) params.assetsJson = JSON.stringify(input.assets);
  return params;
}
