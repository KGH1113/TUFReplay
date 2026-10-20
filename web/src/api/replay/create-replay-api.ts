import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import { toLevelFileResultDto, toReplayStateDto } from "@/api/recorder-dtos";
import { mapReplayLevelFilePickerResult, mapReplayStatus } from "@/models/replay/replay-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import {
  replayLevelFilePickerResultDtoSchema,
  replayStatusDtoSchema,
} from "@/schemas/replay/replay-schema";
import type { ReplayApi } from "./replay-api";
export function createReplayApi(clients: LocalAppChannels): ReplayApi {
  return {
    async play(runId, levelPath) {
      const dto = await callRecorder(clients, (mod) =>
        mod.startReplay(levelPath ? { runId, levelPath } : { runId }),
      );
      return mapReplayStatus(decodeRecorder(replayStatusDtoSchema, toReplayStateDto(dto)));
    },
    async getStatus() {
      return mapReplayStatus(
        decodeRecorder(
          replayStatusDtoSchema,
          toReplayStateDto(await callRecorder(clients, (mod) => mod.readReplayState())),
        ),
      );
    },
    async pickLevelFile(runId, purpose) {
      const dto = await callRecorder(
        clients,
        (mod) => mod.chooseLevelFile({ runId, ...(purpose ? { purpose } : {}) }),
        { timeoutMs: 180_000 },
      );
      return mapReplayLevelFilePickerResult(
        decodeRecorder(replayLevelFilePickerResultDtoSchema, toLevelFileResultDto(dto)),
      );
    },
  };
}
