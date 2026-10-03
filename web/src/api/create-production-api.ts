import { createActivityApi } from "@/api/activity/create-activity-api";
import type { AppApi } from "@/api/app-api";
import { createCalibrationApi } from "@/api/calibration/create-calibration-api";
import { createHealthApi } from "@/api/health/create-health-api";
import { createMicrophoneApi } from "@/api/microphone/create-microphone-api";
import { createRenderApi } from "@/api/render/create-render-api";
import { createReplayApi } from "@/api/replay/create-replay-api";
import { createRunApi } from "@/api/run/create-run-api";
import { createWebcamApi } from "@/api/webcam/create-webcam-api";
import { getAdofaiIpcClients } from "@/shared/clients/adofai-ipc-client";

let apiPromise: Promise<AppApi> | null = null;

export function getProductionApi(): Promise<AppApi> {
  apiPromise ??= getAdofaiIpcClients()
    .then((clients) => ({
      health: createHealthApi(clients),
      activity: createActivityApi(clients),
      run: createRunApi(clients),
      replay: createReplayApi(clients),
      render: createRenderApi(clients),
      microphone: createMicrophoneApi(clients),
      calibration: createCalibrationApi(clients),
      webcam: createWebcamApi(clients),
    }))
    .catch((cause) => {
      apiPromise = null;
      throw cause;
    });
  return apiPromise;
}
