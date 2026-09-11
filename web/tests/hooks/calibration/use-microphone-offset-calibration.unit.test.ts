import { describe, expect, test } from "bun:test";

import type {
  CalibrationGateway,
  LegacyCalibrationStatus as MicrophoneCalibrationStatus,
} from "@/hooks/calibration/use-calibration-gateway-adapter";
import {
  createCalibrationOffsetReconciler,
  createCalibrationOffsetSaveQueue,
  extrapolateCalibrationPlaybackPosition,
  installCalibrationStatusPolling,
  isCalibrationPollingState,
} from "@/hooks/calibration/use-microphone-offset-calibration";

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

  test("keeps the latest optimistic offset while polling and older saves complete", () => {
    const reconciler = createCalibrationOffsetReconciler(0);
    const first = reconciler.begin(80);
    const second = reconciler.begin(140);

    expect(reconciler.synchronize(0)).toBe(140);
    expect(reconciler.resolve(first, 80)).toEqual({ offsetMs: 140, latest: false });
    expect(reconciler.synchronize(80)).toBe(140);
    expect(reconciler.resolve(second, 140)).toEqual({ offsetMs: 140, latest: true });
    expect(reconciler.synchronize(80)).toBe(140);
    expect(reconciler.synchronize(140)).toBe(140);
  });

  test("restores the last confirmed offset when the latest save fails", () => {
    const reconciler = createCalibrationOffsetReconciler(20);
    const first = reconciler.begin(80);
    const second = reconciler.begin(140);

    expect(reconciler.resolve(first, 80)).toEqual({ offsetMs: 140, latest: false });
    expect(reconciler.reject(second)).toEqual({ offsetMs: 80, latest: true });
    expect(reconciler.synchronize(80)).toBe(80);
  });

  test("does not confuse an old matching poll with confirmation of an unsaved commit", () => {
    const reconciler = createCalibrationOffsetReconciler(80);
    const first = reconciler.begin(140);
    const second = reconciler.begin(80);

    expect(reconciler.synchronize(80)).toBe(80);
    expect(reconciler.resolve(first, 140)).toEqual({ offsetMs: 80, latest: false });
    expect(reconciler.synchronize(140)).toBe(80);
    expect(reconciler.resolve(second, 80)).toEqual({ offsetMs: 80, latest: true });
    expect(reconciler.synchronize(140)).toBe(80);
    expect(reconciler.synchronize(80)).toBe(80);
  });
});

describe("microphone calibration status polling", () => {
  test("polls only active states", () => {
    expect(isCalibrationPollingState("waiting_for_run")).toBe(true);
    expect(isCalibrationPollingState("preview_playing")).toBe(true);
    expect(isCalibrationPollingState("idle")).toBe(false);
    expect(isCalibrationPollingState("editing")).toBe(false);
    expect(isCalibrationPollingState("error")).toBe(false);
  });

  test("defers IPC calls while the document is hidden", async () => {
    const scheduled: Array<() => void> = [];
    let visible = false;
    let calls = 0;
    const cleanup = installCalibrationStatusPolling({
      getGateway: () =>
        ({
          getMicrophoneCalibrationStatus: async () => {
            calls += 1;
            return waitingStatus;
          },
        }) as unknown as CalibrationGateway,
      getOperationId: () => "calibration-2",
      onStatus: () => {},
      onError: () => {},
      isVisible: () => visible,
      schedule: (callback) => {
        scheduled.push(callback);
        return scheduled.length as unknown as ReturnType<typeof setTimeout>;
      },
      cancelSchedule: () => {},
    });

    scheduled.shift()?.();
    await Promise.resolve();
    expect(calls).toBe(0);
    expect(scheduled).toHaveLength(1);

    visible = true;
    scheduled.shift()?.();
    await Promise.resolve();
    expect(calls).toBe(1);
    cleanup();
  });

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
    let gateway: CalibrationGateway | null = null;
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
    } as unknown as CalibrationGateway;
    scheduled.shift()?.();
    await Promise.resolve();
    await Promise.resolve();
    expect(statuses).toEqual([waitingStatus]);
    expect(scheduled).toHaveLength(1);
    cleanup();
  });
});
