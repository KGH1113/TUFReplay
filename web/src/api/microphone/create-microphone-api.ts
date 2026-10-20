import type { MicrophoneDevices, MicrophoneTiming } from "@tuf-replay/ipc";
import type { MicrophoneApi } from "@/api/microphone/microphone-api";
import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import { toMicrophoneDevicesDto, toMicrophoneTimingDto } from "@/api/recorder-dtos";
import {
  mapMicrophoneDevicesState,
  mapMicrophoneTimingSettings,
} from "@/models/microphone/microphone-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import {
  microphoneDevicesStateDtoSchema,
  microphoneTimingSettingsDtoSchema,
} from "@/schemas/microphone/microphone-schema";

export function createMicrophoneApi(clients: LocalAppChannels): MicrophoneApi {
  const devices = (value: MicrophoneDevices) =>
    mapMicrophoneDevicesState(
      decodeRecorder(microphoneDevicesStateDtoSchema, toMicrophoneDevicesDto(value)),
    );
  const timing = (value: MicrophoneTiming) =>
    mapMicrophoneTimingSettings(
      decodeRecorder(microphoneTimingSettingsDtoSchema, toMicrophoneTimingDto(value)),
    );
  return {
    getDevices: async () =>
      devices(await callRecorder(clients, (mod) => mod.refreshMicrophoneDevices())),
    setEnabled: async (enabled) =>
      devices(await callRecorder(clients, (mod) => mod.changeMicrophoneAccess({ enabled }))),
    selectDevice: async (deviceId) =>
      devices(await callRecorder(clients, (mod) => mod.chooseMicrophoneDevice({ deviceId }))),
    setOffset: async (offsetMs) =>
      timing(await callRecorder(clients, (mod) => mod.changeMicrophoneOffset({ offsetMs }))),
    setVolume: async (volumeDb) =>
      timing(await callRecorder(clients, (mod) => mod.changeMicrophoneVolume({ volumeDb }))),
  };
}
