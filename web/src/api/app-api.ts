import type { ActivityApi } from "@/api/activity/activity-api";
import type { CalibrationApi } from "@/api/calibration/calibration-api";
import type { HealthApi } from "@/api/health/health-api";
import type { MicrophoneApi } from "@/api/microphone/microphone-api";
import type { ReplayApi } from "@/api/replay/replay-api";
import type { RunApi } from "@/api/run/run-api";

export interface AppApi {
  submission: import("@/api/submission/submission-api").SubmissionApi;
  health: HealthApi;
  activity: ActivityApi;
  run: RunApi;
  replay: ReplayApi;
  microphone: MicrophoneApi;
  calibration: CalibrationApi;
}
