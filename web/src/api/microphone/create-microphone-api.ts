import type { MicrophoneApi } from "@/api/microphone/microphone-api";
import {
  mapMicrophoneDevicesState,
  mapMicrophoneTimingSettings,
} from "@/models/microphone/microphone-model";
import {
  microphoneDevicesStateDtoSchema,
  microphoneTimingSettingsDtoSchema,
} from "@/schemas/microphone/microphone-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";

export function createMicrophoneApi(clients: AdofaiIpcClients): MicrophoneApi {
  return {
    async getDevices() {
      return mapMicrophoneDevicesState(
        await callAdofaiIpc(
          clients.namespace,
          "microphone.devices.get",
          {},
          microphoneDevicesStateDtoSchema,
        ),
      );
    },
    async setEnabled(enabled) {
      return mapMicrophoneDevicesState(
        await callAdofaiIpc(
          clients.namespace,
          "microphone.enabled.set",
          { enabled },
          microphoneDevicesStateDtoSchema,
        ),
      );
    },
    async selectDevice(deviceId) {
      return mapMicrophoneDevicesState(
        await callAdofaiIpc(
          clients.namespace,
          "microphone.device.select",
          { deviceId },
          microphoneDevicesStateDtoSchema,
        ),
      );
    },
    async setOffset(offsetMs) {
      return mapMicrophoneTimingSettings(
        await callAdofaiIpc(
          clients.namespace,
          "microphone.offset.set",
          { offsetMs },
          microphoneTimingSettingsDtoSchema,
        ),
      );
    },
    async setVolume(volumeDb) {
      return mapMicrophoneTimingSettings(
        await callAdofaiIpc(
          clients.namespace,
          "microphone.volume.set",
          { volumeDb },
          microphoneTimingSettingsDtoSchema,
        ),
      );
    },
  };
}
