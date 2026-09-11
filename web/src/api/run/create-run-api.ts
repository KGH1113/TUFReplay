import type { RunApi } from "@/api/run/run-api";
import {
  recordingDeleteResultDtoSchema,
  recordingKeepResultDtoSchema,
  runDeleteResultDtoSchema,
} from "@/schemas/activity/activity-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";

export function createRunApi(clients: AdofaiIpcClients): RunApi {
  return {
    async deleteRun(runId) {
      const result = await callAdofaiIpc(
        clients.namespace,
        "activity.run.delete",
        { runId },
        runDeleteResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Deleted };
    },
    async deleteMicrophoneRecording(runId) {
      const result = await callAdofaiIpc(
        clients.namespace,
        "microphone.recording.delete",
        { runId },
        recordingDeleteResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Deleted };
    },
    async keepMicrophoneRecording(runId) {
      const result = await callAdofaiIpc(
        clients.namespace,
        "microphone.recording.keep",
        { runId },
        recordingKeepResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Permanent };
    },
  };
}
