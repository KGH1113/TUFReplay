import { z } from "zod";
import type { AppEvents } from "@/api/app-events";
import { observeDomainEvent } from "@/api/domain-messages";
import { mapCalibrationStatus } from "@/models/calibration/calibration-model";
import { mapMicrophoneDevicesState } from "@/models/microphone/microphone-model";
import { mapReplayStatus } from "@/models/replay/replay-model";
import type { LocalAppChannels, SessionState } from "@/ports/local-message-peer";
import { calibrationStatusDtoSchema } from "@/schemas/calibration/calibration-schema";
import { microphoneDevicesStateDtoSchema } from "@/schemas/microphone/microphone-schema";
import { replayStatusDtoSchema } from "@/schemas/replay/replay-schema";

export function createAppEvents(channels: LocalAppChannels): AppEvents {
  return {
    on(name, listener) {
      switch (name) {
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
