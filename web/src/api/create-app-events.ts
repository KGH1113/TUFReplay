import { z } from "zod";
import type { AppEvents } from "@/api/app-events";
import { observeDomainEvent } from "@/api/domain-messages";
import { downloadsStateSchema } from "@/api/downloads/downloads-api";
import { mapCalibrationStatus } from "@/models/calibration/calibration-model";
import { mapMicrophoneDevicesState } from "@/models/microphone/microphone-model";
import { mapReplayStatus } from "@/models/replay/replay-model";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import type { LocalAppChannels, SessionState } from "@/ports/local-message-peer";
import { calibrationStatusDtoSchema } from "@/schemas/calibration/calibration-schema";
import { microphoneDevicesStateDtoSchema } from "@/schemas/microphone/microphone-schema";
import {
  outputDirectoryChoiceSchema,
  renderExportStatusSchema,
  renderHealthSchema,
  renderJobSchema,
  renderSettingsSchema,
} from "@/schemas/render/render-schema";
import { replayStatusDtoSchema } from "@/schemas/replay/replay-schema";
import { webcamStateDtoSchema } from "@/schemas/webcam/webcam-schema";

export function createAppEvents(channels: LocalAppChannels): AppEvents {
  return {
    on(name, listener) {
      switch (name) {
        case "downloads.changed":
          return observeDomainEvent(
            channels.namespace,
            "downloads.state.changed",
            downloadsStateSchema,
            listener as (value: import("@/api/downloads/downloads-api").DownloadsState) => void,
          );
        case "render-bundle.changed":
          return observeDomainEvent(
            channels.namespace,
            "render-bundle.state.changed",
            renderExportStatusSchema,
            listener as (value: import("@/models/render/render-model").RenderExportStatus) => void,
          );
        case "renderer.job.changed":
          return channels.rendererNamespace
            ? observeDomainEvent(
                channels.rendererNamespace,
                "renderer.job.changed",
                renderJobSchema,
                listener as (value: import("@/models/render/render-model").RenderJob) => void,
              )
            : () => {};
        case "renderer.settings.changed":
          return channels.rendererNamespace
            ? observeDomainEvent(
                channels.rendererNamespace,
                "renderer.settings.changed",
                renderSettingsSchema,
                listener as (value: import("@/models/render/render-model").RenderSettings) => void,
              )
            : () => {};
        case "renderer.folder.changed":
          return channels.rendererNamespace
            ? observeDomainEvent(
                channels.rendererNamespace,
                "renderer.folder-selection.changed",
                outputDirectoryChoiceSchema,
                listener as (
                  value: import("@/models/render/render-model").OutputDirectorySelection,
                ) => void,
              )
            : () => {};
        case "renderer.health.changed":
          return channels.rendererNamespace
            ? observeDomainEvent(
                channels.rendererNamespace,
                "renderer.health.snapshot",
                renderHealthSchema,
                listener as (value: import("@/models/render/render-model").RenderHealth) => void,
              )
            : () => {};
        case "webcam.changed":
          return observeDomainEvent(
            channels.namespace,
            "webcam.state.changed",
            webcamStateDtoSchema,
            (value) =>
              (listener as (value: ReturnType<typeof mapWebcamState>) => void)(
                mapWebcamState(value),
              ),
          );
        case "activity.changed":
          return observeDomainEvent(
            channels.namespace,
            "activity.changed",
            z.object({ revision: z.number().int(), runId: z.string().nullable() }),
            listener as (value: { revision: number; runId: string | null }) => void,
          );
        case "replay.changed":
          return observeDomainEvent(
            channels.namespace,
            "replay.state.changed",
            replayStatusDtoSchema,
            (value) =>
              (listener as (value: ReturnType<typeof mapReplayStatus>) => void)(
                mapReplayStatus(value),
              ),
          );
        case "calibration.changed":
          return observeDomainEvent(
            channels.namespace,
            "calibration.state.changed",
            calibrationStatusDtoSchema,
            (value) =>
              (listener as (value: ReturnType<typeof mapCalibrationStatus>) => void)(
                mapCalibrationStatus(value),
              ),
          );
        case "microphone.changed":
          return observeDomainEvent(
            channels.namespace,
            "microphone.devices.changed",
            microphoneDevicesStateDtoSchema,
            (value) =>
              (listener as (value: ReturnType<typeof mapMicrophoneDevicesState>) => void)(
                mapMicrophoneDevicesState(value),
              ),
          );
        case "connection.changed": {
          const session = channels.session;
          if (!session) return () => {};
          const receive = listener as (value: SessionState) => void;
          return session.onState(receive);
        }
      }
    },
  };
}
