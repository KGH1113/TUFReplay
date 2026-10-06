import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import type { MicrophoneApi } from "@/api/microphone/microphone-api";
import {
  mapMicrophoneDevicesState,
  mapMicrophoneTimingSettings,
} from "@/models/microphone/microphone-model";
import {
  microphoneDevicesStateDtoSchema,
  microphoneTimingSettingsDtoSchema,
} from "@/schemas/microphone/microphone-schema";

export function createMicrophoneApi(clients: AdofaiIpcClients): MicrophoneApi {
  return {
    async getDevices() {
      return mapMicrophoneDevicesState(
        await sendDomainCommand(
          clients.namespace,
          "microphone.devices.refresh",
          "microphone.devices.changed",
          {},
          microphoneDevicesStateDtoSchema,
        ),
      );
    },
    async setEnabled(enabled) {
      return mapMicrophoneDevicesState(
        await sendDomainCommand(
          clients.namespace,
          "microphone.access.change",
          "microphone.devices.changed",
          { enabled },
          microphoneDevicesStateDtoSchema,
        ),
      );
    },
    async selectDevice(deviceId) {
      return mapMicrophoneDevicesState(
        await sendDomainCommand(
          clients.namespace,
          "microphone.device.choose",
          "microphone.devices.changed",
          { deviceId },
          microphoneDevicesStateDtoSchema,
        ),
      );
    },
    async setOffset(offsetMs) {
      return mapMicrophoneTimingSettings(
        await sendDomainCommand(
          clients.namespace,
          "microphone.offset.change",
          "microphone.timing.changed",
          { offsetMs },
          microphoneTimingSettingsDtoSchema,
        ),
      );
    },
    async setVolume(volumeDb) {
      return mapMicrophoneTimingSettings(
        await sendDomainCommand(
          clients.namespace,
          "microphone.volume.change",
          "microphone.timing.changed",
          { volumeDb },
          microphoneTimingSettingsDtoSchema,
        ),
      );
    },
  };
}
