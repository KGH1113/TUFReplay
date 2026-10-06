import type { DownloadsState } from "@/api/downloads/downloads-api";
import type { CalibrationStatus } from "@/models/calibration/calibration-model";
import type { MicrophoneDevicesState } from "@/models/microphone/microphone-model";
import type {
  OutputDirectorySelection,
  RenderExportStatus,
  RenderHealth,
  RenderJob,
  RenderSettings,
} from "@/models/render/render-model";
import type { ReplayStatus } from "@/models/replay/replay-model";
import type { WebcamState } from "@/models/webcam/webcam-model";
import type { Dispose, NamespaceStatus, SessionState } from "@/ports/local-message-peer";

export interface AppEventMap {
  "recorder.status.changed": NamespaceStatus;
  "renderer.status.changed": NamespaceStatus;
  "downloads.changed": DownloadsState;
  "render-bundle.changed": RenderExportStatus;
  "renderer.job.changed": RenderJob;
  "renderer.settings.changed": RenderSettings;
  "renderer.folder.changed": OutputDirectorySelection;
  "renderer.health.changed": RenderHealth;
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
