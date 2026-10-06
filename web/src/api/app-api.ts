import type { ActivityApi } from "@/api/activity/activity-api";
import type { AppEvents } from "@/api/app-events";
import type { CalibrationApi } from "@/api/calibration/calibration-api";
import type { HealthApi } from "@/api/health/health-api";
import type { MicrophoneApi } from "@/api/microphone/microphone-api";
import type { ReplayApi } from "@/api/replay/replay-api";
import type { RunApi } from "@/api/run/run-api";
import type { WebcamApi } from "@/api/webcam/webcam-api";

export interface AppApi {
  events: AppEvents;
  health: HealthApi;
  activity: ActivityApi;
  run: RunApi;
  replay: ReplayApi;
  microphone: MicrophoneApi;
  calibration: CalibrationApi;
  webcam: WebcamApi;
}
