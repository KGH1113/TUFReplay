import type { RunApi } from "@/api/run/run-api";
import {
  recordingDeleteResultDtoSchema,
  recordingDownloadTicketDtoSchema,
  recordingKeepResultDtoSchema,
  runDeleteResultDtoSchema,
} from "@/schemas/activity/activity-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";

export function createRunApi(clients: AdofaiIpcClients): RunApi {
  return {
    async prepareMicrophoneRecordingDownload(runId) {
      const ticket = await callAdofaiIpc(
        clients.namespace,
        "microphone.recording.export",
        { runId },
        recordingDownloadTicketDtoSchema,
      );
      const url = new URL(ticket.Url);
      if (
        url.protocol !== "http:" ||
        url.hostname !== "127.0.0.1" ||
        Number(url.port) < 32145 ||
        Number(url.port) > 32155 ||
        url.username !== "" ||
        url.password !== "" ||
        url.search !== "" ||
        url.hash !== "" ||
        !/^\/ipc\/download\/[A-Za-z0-9_-]+$/.test(url.pathname)
      )
        throw new Error("Invalid local microphone download URL.");
      return ticket.Url;
    },
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
