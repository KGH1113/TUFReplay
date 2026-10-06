import { createLazyAppChannels } from "@/adapters/adofai-ipc/connection";
import { createLocalTextDownloads } from "@/adapters/adofai-ipc/text-download";
import { createActivityApi } from "@/api/activity/create-activity-api";
import type { AppApi } from "@/api/app-api";
import { createCalibrationApi } from "@/api/calibration/create-calibration-api";
import { createAppEvents } from "@/api/create-app-events";
import { createHealthApi } from "@/api/health/create-health-api";
import { createMicrophoneApi } from "@/api/microphone/create-microphone-api";
import { createReplayApi } from "@/api/replay/create-replay-api";
import { createRunApi } from "@/api/run/create-run-api";
import { createWebcamApi } from "@/api/webcam/create-webcam-api";

let apiPromise: Promise<AppApi> | null = null;

export function getProductionApi(): Promise<AppApi> {
  if (!apiPromise) {
    const clients = createLazyAppChannels();
    apiPromise = Promise.resolve({
      events: createAppEvents(clients),
      health: createHealthApi(clients),
      activity: createActivityApi(clients, createLocalTextDownloads()),
      run: createRunApi(clients),
      replay: createReplayApi(clients),
      microphone: createMicrophoneApi(clients),
      calibration: createCalibrationApi(clients),
      webcam: createWebcamApi(clients),
    });
  }
  return apiPromise;
}
