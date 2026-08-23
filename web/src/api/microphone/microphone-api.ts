import type {
  MicrophoneDevicesState,
  MicrophoneTimingSettings,
} from "@/models/microphone/microphone-model";

export interface MicrophoneApi {
  getDevices(): Promise<MicrophoneDevicesState>;
  setEnabled(enabled: boolean): Promise<MicrophoneDevicesState>;
  selectDevice(deviceId: string | null): Promise<MicrophoneDevicesState>;
  setOffset(offsetMs: number): Promise<MicrophoneTimingSettings>;
  setVolume(volumeDb: number): Promise<MicrophoneTimingSettings>;
}
