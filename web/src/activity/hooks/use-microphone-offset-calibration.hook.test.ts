import { describe, expect, test } from "bun:test";

import type { MicrophoneCalibrationStatus } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import {
  createCalibrationOffsetSaveQueue,
  extrapolateCalibrationPlaybackPosition,
  installCalibrationStatusPolling,
} from "./use-microphone-offset-calibration.hook";

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

describe("microphone calibration offset saving", () => {
  test("waits for the final offset save before closing calibration", async () => {
    const events: string[] = [];
    let finishSave = () => {};
    const queue = createCalibrationOffsetSaveQueue(() => {});
    queue.enqueue(
      () =>
        new Promise<void>((resolve) => {
          events.push("save started");
          finishSave = resolve;
        }),
    );

    const close = queue.flush().then(() => events.push("closed"));
    await Promise.resolve();
    expect(events).toEqual(["save started"]);

    finishSave();
    await close;
    expect(events).toEqual(["save started", "closed"]);
  });
});

describe("microphone calibration status polling", () => {
  test("keeps the playhead at zero until gameplay starts after countdown", () => {
    expect(
      extrapolateCalibrationPlaybackPosition({ positionMs: 0, sampledAtMs: 1_000 }, 2_500, 6_000),
    ).toBe(0);
    expect(
      extrapolateCalibrationPlaybackPosition({ positionMs: 125, sampledAtMs: 1_000 }, 1_250, 6_000),
    ).toBe(375);
  });

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
