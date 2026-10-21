import { describe, expect, test } from "bun:test";

import type {
  CalibrationGateway,
  LegacyCalibrationStatus as MicrophoneCalibrationStatus,
} from "@/hooks/calibration/use-calibration-gateway-adapter";
import {
  createCalibrationOffsetReconciler,
  createCalibrationOffsetSaveQueue,
  extrapolateCalibrationPlaybackPosition,
  installCalibrationStatusSubscription,
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

  test("keeps the latest optimistic offset while state messages arrive and older saves complete", () => {
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

  test("does not confuse an old matching state message with confirmation of an unsaved commit", () => {
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

describe("microphone calibration status subscriptions", () => {
  test("accepts only the active operation and stops consuming messages on cleanup", async () => {
    let receive = (_status: MicrophoneCalibrationStatus) => {};
    let unsubscribeCount = 0;
    const gateway = {
      subscribeMicrophoneCalibrationStatus(listener: typeof receive) {
        receive = listener;
        return () => {
          unsubscribeCount += 1;
        };
      },
    } as unknown as CalibrationGateway;
    const statuses: MicrophoneCalibrationStatus[] = [];
    const cleanup = installCalibrationStatusSubscription({
      getGateway: () => gateway,
      getOperationId: () => "calibration-2",
      onStatus: (status) => {
        statuses.push(status);
      },
      onError: () => {},
    });
    receive({ ...waitingStatus, OperationId: "older-operation" });
    receive(waitingStatus);
    await Promise.resolve();
    expect(statuses).toEqual([waitingStatus]);
    cleanup();
    receive(waitingStatus);
    expect(statuses).toHaveLength(1);
    expect(unsubscribeCount).toBe(1);
  });

  test("keeps the playhead at zero until gameplay starts after countdown", () => {
    expect(
      extrapolateCalibrationPlaybackPosition({ positionMs: 0, sampledAtMs: 1_000 }, 2_500, 6_000),
    ).toBe(0);
    expect(
      extrapolateCalibrationPlaybackPosition({ positionMs: 125, sampledAtMs: 1_000 }, 1_250, 6_000),
    ).toBe(375);
  });

  test("reports failed asynchronous state consumers without leaving subscriptions active", async () => {
    let receive = (_status: MicrophoneCalibrationStatus) => {};
    const error = new Error("Waveform unavailable");
    const errors: unknown[] = [];
    const cleanup = installCalibrationStatusSubscription({
      getGateway: () =>
        ({
          subscribeMicrophoneCalibrationStatus(listener: typeof receive) {
            receive = listener;
            return () => {};
          },
        }) as unknown as CalibrationGateway,
      getOperationId: () => "calibration-2",
      onStatus: async () => {
        throw error;
      },
      onError: (cause) => {
        errors.push(cause);
      },
    });
    receive(waitingStatus);
    await Promise.resolve();
    await Promise.resolve();
    expect(errors).toEqual([error]);
    cleanup();
  });
});
