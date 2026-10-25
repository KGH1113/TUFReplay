import type {
  MicrophoneDevicesStateDto,
  MicrophoneTimingSettingsDto,
} from "@/schemas/microphone/microphone-schema";

export type MicrophoneDevice = ReturnType<typeof mapMicrophoneDevice>;
export type MicrophoneDevicesState = ReturnType<typeof mapMicrophoneDevicesState>;
export type MicrophoneTimingSettings = ReturnType<typeof mapMicrophoneTimingSettings>;

export function mapMicrophoneDevice(dto: MicrophoneDevicesStateDto["Devices"][number]) {
  return {
    id: dto.Id,
    name: dto.Name,
    minFrequency: dto.MinFrequency,
    maxFrequency: dto.MaxFrequency,
    bluetooth: dto.Bluetooth,
  };
}

export function mapMicrophoneDevicesState(dto: MicrophoneDevicesStateDto) {
  return {
    enabled: dto.Enabled,
    toggleLocked: dto.ToggleLocked,
    devices: dto.Devices.map(mapMicrophoneDevice),
    defaultDeviceBluetooth: dto.DefaultDeviceBluetooth,
    selectedDeviceId: dto.SelectedDeviceId,
    microphoneOffsetMs: dto.MicrophoneOffsetMs,
    microphoneVolumeDb: dto.MicrophoneVolumeDb,
  };
}

export function mapMicrophoneTimingSettings(dto: MicrophoneTimingSettingsDto) {
  return {
    microphoneOffsetMs: dto.MicrophoneOffsetMs,
    microphoneVolumeDb: dto.MicrophoneVolumeDb,
  };
}

/** A Bluetooth headset microphone switches the headset to its lower-quality call mode. */
export function recordsFromBluetooth(state: {
  devices: MicrophoneDevice[];
  defaultDeviceBluetooth: boolean;
  selectedDeviceId: string | null;
}) {
  if (state.selectedDeviceId === null) return state.defaultDeviceBluetooth;
  return state.devices.some((device) => device.id === state.selectedDeviceId && device.bluetooth);
}
