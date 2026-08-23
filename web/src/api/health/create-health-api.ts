import type { HealthApi } from "@/api/health/health-api";
import { mapHealth } from "@/models/health/health-model";
import { healthDtoSchema } from "@/schemas/health/health-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";
import { ApiError } from "@/shared/errors/api-error";

export const SUPPORTED_PROTOCOL_VERSION = 6;

export function createHealthApi(clients: AdofaiIpcClients): HealthApi {
  return {
    async get() {
      const health = mapHealth(
        await callAdofaiIpc(clients.namespace, "health.get", {}, healthDtoSchema),
      );
      if (health.protocolVersion !== SUPPORTED_PROTOCOL_VERSION) {
        throw new ApiError(
          `TUFReplay IPC protocol mismatch (expected ${SUPPORTED_PROTOCOL_VERSION}, detected ${health.protocolVersion}, mod ${health.modVersion}).`,
          { kind: "protocol", code: "protocol_mismatch" },
        );
      }
      return health;
    },
  };
}
