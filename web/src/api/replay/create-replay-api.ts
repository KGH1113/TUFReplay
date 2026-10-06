import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import {
  mapReplayLevelFilePickerResult,
  mapReplayStatus,
  type ReplayLevelFilePickerResult,
} from "@/models/replay/replay-model";
import {
  replayLevelFilePickerResultDtoSchema,
  replayStatusDtoSchema,
} from "@/schemas/replay/replay-schema";
import type { ReplayApi } from "./replay-api";

export function createReplayApi(clients: AdofaiIpcClients): ReplayApi {
  return {
    async play(runId, levelPath) {
      const dto = await sendDomainCommand(
        clients.namespace,
        "replay.start",
        "replay.state.changed",
        levelPath ? { runId, levelPath } : { runId },
        replayStatusDtoSchema,
      );
      return mapReplayStatus(dto);
    },
    async getStatus() {
      return mapReplayStatus(
        await sendDomainCommand(
          clients.namespace,
          "replay.state.read",
          "replay.state.changed",
          {},
          replayStatusDtoSchema,
        ),
      );
    },
    async pickLevelFile(runId): Promise<ReplayLevelFilePickerResult> {
      const dto = await sendDomainCommand(
        clients.namespace,
        "replay.level-file.choose",
        "replay.level-file.finished",
        { runId },
        replayLevelFilePickerResultDtoSchema,
        { timeoutMs: 180_000 },
      );
      return mapReplayLevelFilePickerResult(dto);
    },
  };
}
