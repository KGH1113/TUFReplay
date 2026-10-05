import type { ActivityApi } from "@/api/activity/activity-api";
import type { CalibrationApi } from "@/api/calibration/calibration-api";
import type { DownloadsApi } from "@/api/downloads/downloads-api";
import type { HealthApi } from "@/api/health/health-api";
import type { MicrophoneApi } from "@/api/microphone/microphone-api";
import type { RenderApi } from "@/api/render/render-api";
import type { ReplayApi } from "@/api/replay/replay-api";
import type { RunApi } from "@/api/run/run-api";
import type { WebcamApi } from "@/api/webcam/webcam-api";

export interface AppApi {
  health: HealthApi;
  activity: ActivityApi;
  run: RunApi;
  replay: ReplayApi;
  microphone: MicrophoneApi;
  calibration: CalibrationApi;
  webcam: WebcamApi;
  render?: RenderApi;
  downloads?: DownloadsApi;
}
