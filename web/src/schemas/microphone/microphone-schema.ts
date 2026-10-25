import { z } from "zod";

export const microphoneDeviceDtoSchema = z.object({
  Id: z.string(),
  Name: z.string(),
  MinFrequency: z.number(),
  MaxFrequency: z.number(),
  // Older recorders do not report the transport.
  Bluetooth: z.boolean().default(false),
});

export const microphoneDevicesStateDtoSchema = z
  .object({
    Enabled: z.boolean(),
    ToggleLocked: z.boolean(),
    Devices: z.array(microphoneDeviceDtoSchema),
    DefaultDeviceBluetooth: z.boolean().default(false),
    SelectedDeviceId: z.string().nullable(),
    MicrophoneOffsetMs: z.number(),
    MicrophoneVolumeDb: z.number(),
  })
  .passthrough();

export const microphoneTimingSettingsDtoSchema = z.object({
  MicrophoneOffsetMs: z.number(),
  MicrophoneVolumeDb: z.number(),
});

export type MicrophoneDevicesStateDto = z.infer<typeof microphoneDevicesStateDtoSchema>;
export type MicrophoneTimingSettingsDto = z.infer<typeof microphoneTimingSettingsDtoSchema>;
