import type { HealthApi } from "@/api/health/health-api";
import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import { toHealthDto } from "@/api/recorder-dtos";
import { mapHealth } from "@/models/health/health-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import { healthDtoSchema } from "@/schemas/health/health-schema";
import { ApiError } from "@/shared/errors/api-error";
export const SUPPORTED_PROTOCOL_VERSION = 13;
export function createHealthApi(clients: LocalAppChannels): HealthApi {
  return {
    async get() {
      const health = mapHealth(
        decodeRecorder(
          healthDtoSchema,
          toHealthDto(await callRecorder(clients, (mod) => mod.readHealth())),
        ),
      );
      if (health.protocolVersion !== SUPPORTED_PROTOCOL_VERSION)
        throw new ApiError(
          `TUFReplay version mismatch (expected protocol ${SUPPORTED_PROTOCOL_VERSION}, detected ${health.protocolVersion}, mod ${health.modVersion}).`,
          { kind: "protocol", code: "protocol_mismatch" },
        );
      return health;
    },
  };
}
