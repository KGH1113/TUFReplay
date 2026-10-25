import { describe, expect, test } from "bun:test";

import {
  mapMicrophoneDevicesState,
  recordsFromBluetooth,
} from "@/models/microphone/microphone-model";
import { microphoneDevicesStateDtoSchema } from "@/schemas/microphone/microphone-schema";

const devices = [
  {
    id: "built-in",
    name: "Built-in",
    minFrequency: 48_000,
    maxFrequency: 48_000,
    bluetooth: false,
  },
  { id: "airpods", name: "AirPods", minFrequency: 24_000, maxFrequency: 24_000, bluetooth: true },
];

describe("Bluetooth microphone warning", () => {
  test("warns for a selected Bluetooth device only", () => {
    expect(
      recordsFromBluetooth({ devices, defaultDeviceBluetooth: false, selectedDeviceId: "airpods" }),
    ).toBe(true);
    expect(
      recordsFromBluetooth({ devices, defaultDeviceBluetooth: true, selectedDeviceId: "built-in" }),
    ).toBe(false);
  });

  test("follows the system default when no device is selected", () => {
    expect(
      recordsFromBluetooth({ devices, defaultDeviceBluetooth: true, selectedDeviceId: null }),
    ).toBe(true);
    expect(
      recordsFromBluetooth({ devices, defaultDeviceBluetooth: false, selectedDeviceId: null }),
    ).toBe(false);
  });

  test("treats recorders without transport data as non-Bluetooth", () => {
    const state = mapMicrophoneDevicesState(
      microphoneDevicesStateDtoSchema.parse({
        Enabled: true,
        ToggleLocked: false,
        Devices: [{ Id: "mic", Name: "Mic", MinFrequency: 48_000, MaxFrequency: 48_000 }],
        SelectedDeviceId: null,
        MicrophoneOffsetMs: 0,
        MicrophoneVolumeDb: 0,
      }),
    );
    expect(state.defaultDeviceBluetooth).toBe(false);
    expect(state.devices[0]?.bluetooth).toBe(false);
  });
});
