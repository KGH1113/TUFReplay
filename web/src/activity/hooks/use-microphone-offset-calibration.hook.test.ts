import { describe, expect, test } from "bun:test";

import type { MicrophoneCalibrationStatus } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { installCalibrationStatusPolling } from "./use-microphone-offset-calibration.hook";

const waitingStatus: MicrophoneCalibrationStatus = {
  OperationId: "calibration-2",
  State: "waiting_for_run",
  ErrorCode: null,
  Message: null,
  DurationMs: 0,
  PlaybackPositionMs: 0,
  ResultRevision: 0,
  MicrophoneOffsetMs: 0,
  MicrophoneVolumeDb: 0,
};

describe("microphone calibration status polling", () => {
  test("keeps polling when the shared IPC gateway temporarily disappears", async () => {
    const scheduled: Array<() => void> = [];
    let gateway: ActivityGateway | null = null;
    const statuses: MicrophoneCalibrationStatus[] = [];
    const cleanup = installCalibrationStatusPolling({
      getGateway: () => gateway,
      getOperationId: () => "calibration-2",
      onStatus: (status) => {
        statuses.push(status);
      },
      onError: () => {},
      schedule: (callback) => {
        scheduled.push(callback);
        return scheduled.length as unknown as ReturnType<typeof setTimeout>;
      },
      cancelSchedule: () => {},
    });

    scheduled.shift()?.();
    await Promise.resolve();
    expect(scheduled).toHaveLength(1);
    expect(statuses).toHaveLength(0);

    gateway = {
      getMicrophoneCalibrationStatus: async () => waitingStatus,
    } as unknown as ActivityGateway;
    scheduled.shift()?.();
    await Promise.resolve();
    await Promise.resolve();
    expect(statuses).toEqual([waitingStatus]);
    expect(scheduled).toHaveLength(1);
    cleanup();
  });
});
