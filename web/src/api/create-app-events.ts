import type { AppEvents } from "@/api/app-events";
import { observeDomainEvent } from "@/api/domain-messages";
import { downloadsStateSchema } from "@/api/downloads/downloads-api";
import { decodeRecorder } from "@/api/recorder-call";
import {
  toCalibrationStateDto,
  toDownloadsStateDto,
  toMicrophoneDevicesDto,
  toReplayStateDto,
  toWebcamStateDto,
} from "@/api/recorder-dtos";
import { mapCalibrationStatus } from "@/models/calibration/calibration-model";
import { mapMicrophoneDevicesState } from "@/models/microphone/microphone-model";
import { mapReplayStatus } from "@/models/replay/replay-model";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import type { LocalAppChannels, NamespaceStatus, SessionState } from "@/ports/local-message-peer";
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
        case "recorder.status.changed":
          return channels.recorder.onStatus(listener as (value: NamespaceStatus) => void);
        case "renderer.status.changed":
          return (
            channels.rendererNamespace?.onStatus?.(listener as (value: NamespaceStatus) => void) ??
            (() => {})
          );
        case "downloads.changed":
          return channels.recorder.on("downloadsStateChanged", (value) =>
            (listener as (value: import("@/api/downloads/downloads-api").DownloadsState) => void)(
              decodeRecorder(downloadsStateSchema, toDownloadsStateDto(value)),
            ),
          );
        case "render-bundle.changed":
          return channels.recorder.on("renderBundleStateChanged", (value) =>
            (
              listener as (value: import("@/models/render/render-model").RenderExportStatus) => void
            )(decodeRecorder(renderExportStatusSchema, value)),
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
          return channels.recorder.on("webcamStateChanged", (value) =>
            (listener as (value: ReturnType<typeof mapWebcamState>) => void)(
              mapWebcamState(decodeRecorder(webcamStateDtoSchema, toWebcamStateDto(value))),
            ),
          );
        case "activity.changed":
          return channels.recorder.on(
            "activityChanged",
            listener as (value: { revision: number; runId: string | null }) => void,
          );
        case "replay.changed":
          return channels.recorder.on("replayStateChanged", (value) =>
            (listener as (value: ReturnType<typeof mapReplayStatus>) => void)(
              mapReplayStatus(decodeRecorder(replayStatusDtoSchema, toReplayStateDto(value))),
            ),
          );
        case "calibration.changed":
          return channels.recorder.on("calibrationStateChanged", (value) =>
            (listener as (value: ReturnType<typeof mapCalibrationStatus>) => void)(
              mapCalibrationStatus(
                decodeRecorder(calibrationStatusDtoSchema, toCalibrationStateDto(value)),
              ),
            ),
          );
        case "microphone.changed":
          return channels.recorder.on("microphoneDevicesChanged", (value) =>
            (listener as (value: ReturnType<typeof mapMicrophoneDevicesState>) => void)(
              mapMicrophoneDevicesState(
                decodeRecorder(microphoneDevicesStateDtoSchema, toMicrophoneDevicesDto(value)),
              ),
            ),
          );
        case "connection.changed":
          return channels.session?.onState(listener as (value: SessionState) => void) ?? (() => {});
      }
    },
  };
}
