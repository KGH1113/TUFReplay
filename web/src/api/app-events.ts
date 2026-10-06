import type { CalibrationStatus } from "@/models/calibration/calibration-model";
import type { MicrophoneDevicesState } from "@/models/microphone/microphone-model";
import type { ReplayStatus } from "@/models/replay/replay-model";
import type { WebcamState } from "@/models/webcam/webcam-model";
import type { Dispose, SessionState } from "@/ports/local-message-peer";

export interface AppEventMap {
  "webcam.changed": WebcamState;
  "activity.changed": { revision: number; runId: string | null };
  "replay.changed": ReplayStatus;
  "calibration.changed": CalibrationStatus;
  "microphone.changed": MicrophoneDevicesState;
  "connection.changed": SessionState;
}

export interface AppEvents {
  on<K extends keyof AppEventMap>(name: K, listener: (value: AppEventMap[K]) => void): Dispose;
}
