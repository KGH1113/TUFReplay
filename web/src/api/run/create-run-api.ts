import { z } from "zod";
import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import type { RunApi } from "@/api/run/run-api";
import {
  recordingDeleteResultDtoSchema,
  recordingKeepResultDtoSchema,
  runDeleteResultDtoSchema,
} from "@/schemas/activity/activity-schema";

export function createRunApi(clients: AdofaiIpcClients): RunApi {
  return {
    async prepareMicrophoneRecordingDownload(runId) {
      const ticket = await sendDomainCommand(
        clients.namespace,
        "microphone.recording.download",
        "download.ready",
        { runId },
        z.object({ url: z.string(), byteLength: z.number().optional() }),
      );
      const url = new URL(ticket.url);
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
      return ticket.url;
    },
    async deleteRun(runId) {
      const result = await sendDomainCommand(
        clients.namespace,
        "activity.run.remove",
        "activity.run.removed",
        { runId },
        runDeleteResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Deleted };
    },
    async deleteMicrophoneRecording(runId) {
      const result = await sendDomainCommand(
        clients.namespace,
        "microphone.recording.remove",
        "microphone.recording.removed",
        { runId },
        recordingDeleteResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Deleted };
    },
    async keepMicrophoneRecording(runId) {
      const result = await sendDomainCommand(
        clients.namespace,
        "microphone.recording.retain",
        "microphone.recording.retained",
        { runId },
        recordingKeepResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Permanent };
    },
  };
}
