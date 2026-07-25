import { describe, expect, test } from "bun:test";

import type {
  MicrophoneCalibrationResult,
  MicrophoneCalibrationStatus,
  MicrophoneDevicesState,
  ReplayLevelFilePickerResult,
  ReplayStatus,
} from "../activity.model";
import { ActivityDomainError, createActivityGateway, loadAllPages } from "./activity.gateway";

describe("activity IPC contract", () => {
  test("paging consumes raw arrays and continues past 1000 until a short page", async () => {
    const source = Array.from({ length: 1_237 }, (_, index) => index);
    const offsets: number[] = [];
    const result = await loadAllPages(async (offset, limit) => {
      offsets.push(offset);
      return source.slice(offset, offset + limit);
    });
    expect(result).toEqual(source);
    expect(offsets).toEqual([0, 200, 400, 600, 800, 1000, 1200]);
  });

  test("successful HTTP domain-error payloads become useful typed errors", async () => {
    const namespace = {
      call: async () => ({ error: { code: "not_found", message: "Session is missing" } }),
    };
    const gateway = createActivityGateway(namespace as never);
    try {
      await gateway.getLevelSession("missing");
      throw new Error("expected domain error");
    } catch (error) {
      expect(error).toBeInstanceOf(ActivityDomainError);
      expect((error as ActivityDomainError).code).toBe("not_found");
      expect((error as Error).message).toBe("Session is missing");
    }
  });

  test("uses logical-level endpoints for cross-visit activity", async () => {
    const calls: Array<{ method: string; params: unknown }> = [];
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return method.endsWith("runs.list") ? [] : {};
      },
    };
    const gateway = createActivityGateway(namespace as never);
    await gateway.getLogicalLevel("logical-1");
    await gateway.listAllLogicalLevelRuns("logical-1");
    await gateway.getLogicalLevelChart("logical-1");
    expect(calls).toEqual([
      { method: "activity.logical-level.get", params: { id: "logical-1" } },
      {
        method: "activity.logical-level.runs.list",
        params: { id: "logical-1", offset: 0, limit: 200 },
      },
      { method: "activity.logical-level.chart.get", params: { id: "logical-1" } },
    ]);
  });

  test("deletes a microphone recording with the exact command and params", async () => {
    const calls: Array<{ method: string; params: unknown }> = [];
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return { RunId: "run-7", Deleted: true };
      },
    };
    const gateway = createActivityGateway(namespace as never);

    expect(await gateway.deleteMicrophoneRecording("run-7")).toEqual({
      RunId: "run-7",
      Deleted: true,
    });
    expect(calls).toEqual([{ method: "microphone.recording.delete", params: { runId: "run-7" } }]);
  });

  test("keeps a microphone recording permanently with the exact command and params", async () => {
    const calls: Array<{ method: string; params: unknown }> = [];
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return { RunId: "run-8", Permanent: true };
      },
    };
    const gateway = createActivityGateway(namespace as never);

    expect(await gateway.keepMicrophoneRecording("run-8")).toEqual({
      RunId: "run-8",
      Permanent: true,
    });
    expect(calls).toEqual([{ method: "microphone.recording.keep", params: { runId: "run-8" } }]);
  });

  test("uses the exact replay command names and params", async () => {
    const calls: Array<{ method: string; params: unknown }> = [];
    const status = {
      OperationId: "op-1",
      RunId: "run-1",
      State: "preparing",
      ErrorCode: null,
      Message: null,
    } satisfies ReplayStatus;
    const pickerResult = {
      RunId: "run-1",
      Outcome: "selected",
      LevelPath: "/levels/replay.adofai",
      ErrorCode: null,
      Message: null,
    } satisfies ReplayLevelFilePickerResult;
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return method.includes("level-file") ? pickerResult : status;
      },
    };
    const gateway = createActivityGateway(namespace as never);

    expect(await gateway.playReplay("run-1")).toBe(status);
    expect(await gateway.playReplay("run-1", "/levels/replay.adofai")).toBe(status);
    expect(await gateway.getReplayStatus()).toBe(status);
    expect(await gateway.pickReplayLevelFile("run-1")).toBe(pickerResult);
    expect(calls).toEqual([
      { method: "replay.play", params: { runId: "run-1" } },
      {
        method: "replay.play",
        params: { runId: "run-1", levelPath: "/levels/replay.adofai" },
      },
      { method: "replay.status.get", params: {} },
      { method: "replay.level-file.pick", params: { runId: "run-1" } },
    ]);
  });

  test("uses the exact microphone command names and nullable selection param", async () => {
    const calls: Array<{ method: string; params: unknown }> = [];
    const microphones = {
      Enabled: true,
      ToggleLocked: false,
      Devices: [
        {
          Id: "USB Audio Device",
          Name: "USB Audio Device",
          MinFrequency: 44_100,
          MaxFrequency: 48_000,
        },
      ],
      SelectedDeviceId: null,
    } satisfies MicrophoneDevicesState;
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return microphones;
      },
    };
    const gateway = createActivityGateway(namespace as never);

    expect(await gateway.getMicrophoneDevices()).toBe(microphones);
    expect(await gateway.setMicrophoneEnabled(false)).toBe(microphones);
    expect(await gateway.selectMicrophoneDevice("USB Audio Device")).toBe(microphones);
    expect(await gateway.selectMicrophoneDevice(null)).toBe(microphones);
    expect(calls).toEqual([
      { method: "microphone.devices.get", params: {} },
      { method: "microphone.enabled.set", params: { enabled: false } },
      { method: "microphone.device.select", params: { deviceId: "USB Audio Device" } },
      { method: "microphone.device.select", params: { deviceId: null } },
    ]);
  });

  test("uses the exact microphone calibration command names and operation guards", async () => {
    const calls: Array<{ method: string; params: unknown }> = [];
    const status = {
      OperationId: "calibration-1",
      State: "editing",
      ErrorCode: null,
      Message: null,
      DurationMs: 6000,
      PlaybackPositionMs: 0,
      ResultRevision: 1,
      MicrophoneOffsetMs: 24,
      MicrophoneVolumeDb: 6,
    } satisfies MicrophoneCalibrationStatus;
    const result = {
      OperationId: "calibration-1",
      Revision: 1,
      DurationMs: 6000,
      GameWaveform: [0, 1],
      SongWaveform: [0, 0.5],
      MicrophoneWaveform: [1, 0],
    } satisfies MicrophoneCalibrationResult;
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return method === "microphone.calibration.result.get" ? result : status;
      },
    };
    const gateway = createActivityGateway(namespace as never);

    await gateway.startMicrophoneCalibration();
    await gateway.getMicrophoneCalibrationStatus("calibration-1");
    await gateway.getMicrophoneCalibrationResult("calibration-1", 1);
    await gateway.playMicrophoneCalibrationPreview("calibration-1");
    await gateway.stopMicrophoneCalibrationPreview("calibration-1");
    await gateway.setMicrophoneCalibrationOffset("calibration-1", 24);
    await gateway.setMicrophoneCalibrationVolume("calibration-1", 6);
    await gateway.closeMicrophoneCalibration("calibration-1");

    expect(calls).toEqual([
      { method: "microphone.calibration.start", params: {} },
      {
        method: "microphone.calibration.status.get",
        params: { operationId: "calibration-1" },
      },
      {
        method: "microphone.calibration.result.get",
        params: { operationId: "calibration-1", revision: 1 },
      },
      {
        method: "microphone.calibration.preview.play",
        params: { operationId: "calibration-1" },
      },
      {
        method: "microphone.calibration.preview.stop",
        params: { operationId: "calibration-1" },
      },
      {
        method: "microphone.calibration.offset.set",
        params: { operationId: "calibration-1", offsetMs: 24 },
      },
      {
        method: "microphone.calibration.volume.set",
        params: { operationId: "calibration-1", volumeDb: 6 },
      },
      {
        method: "microphone.calibration.close",
        params: { operationId: "calibration-1" },
      },
    ]);
  });
});
