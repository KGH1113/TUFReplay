import { z } from "zod";
import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import type { RunApi } from "@/api/run/run-api";
import {
  recordingDeleteResultDtoSchema,
  recordingKeepResultDtoSchema,
  runDeleteResultDtoSchema,
} from "@/schemas/activity/activity-schema";

export function createRunApi(clients: AdofaiIpcClients): RunApi {
  async function prepareRecordingDownload(runId: string, kind: "microphone" | "camera") {
    const ticket = await sendDomainCommand(
      clients.namespace,
      `${kind}.recording.download`,
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
      throw new Error(`Invalid local ${kind} download URL.`);
    return ticket.url;
  }

  return {
    prepareMicrophoneRecordingDownload: (runId) => prepareRecordingDownload(runId, "microphone"),
    prepareCameraRecordingDownload: (runId) => prepareRecordingDownload(runId, "camera"),
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
    async deleteCameraRecording(runId) {
      const result = await sendDomainCommand(
        clients.namespace,
        "camera.recording.remove",
        "camera.recording.removed",
        { runId },
        recordingDeleteResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Deleted };
    },
    async keepCameraRecording(runId) {
      const result = await sendDomainCommand(
        clients.namespace,
        "camera.recording.retain",
        "camera.recording.retained",
        { runId },
        recordingKeepResultDtoSchema,
      );
      return { runId: result.RunId, changed: result.Permanent };
    },
  };
}
