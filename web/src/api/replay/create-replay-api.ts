import {
  mapReplayLevelFilePickerResult,
  mapReplayStatus,
  type ReplayLevelFilePickerResult,
} from "@/models/replay/replay-model";
import {
  replayLevelFilePickerResultDtoSchema,
  replayStatusDtoSchema,
} from "@/schemas/replay/replay-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";
import type { ReplayApi } from "./replay-api";

const FILE_PICKER_POLL_INTERVAL_MS = 100;

export function createReplayApi(clients: AdofaiIpcClients): ReplayApi {
  return {
    async play(runId, levelPath) {
      const dto = await callAdofaiIpc(
        clients.namespace,
        "replay.play",
        levelPath ? { runId, levelPath } : { runId },
        replayStatusDtoSchema,
      );
      return mapReplayStatus(dto);
    },

    async getStatus() {
      const dto = await callAdofaiIpc(
        clients.namespace,
        "replay.status.get",
        {},
        replayStatusDtoSchema,
      );
      return mapReplayStatus(dto);
    },

    async pickLevelFile(runId): Promise<ReplayLevelFilePickerResult> {
      let result = await pick(clients, "replay.level-file.pick", { runId });
      while (result.outcome === "picking" && result.operationId) {
        await delay(FILE_PICKER_POLL_INTERVAL_MS);
        result = await pick(clients, "replay.level-file.status.get", {
          operationId: result.operationId,
        });
      }
      return result;
    },
  };
}

async function pick(
  clients: AdofaiIpcClients,
  method: string,
  params: object,
): Promise<ReplayLevelFilePickerResult> {
  const dto = await callAdofaiIpc(
    clients.pickerNamespace,
    method,
    params,
    replayLevelFilePickerResultDtoSchema,
  );
  return mapReplayLevelFilePickerResult(dto);
}

function delay(milliseconds: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, milliseconds));
}
