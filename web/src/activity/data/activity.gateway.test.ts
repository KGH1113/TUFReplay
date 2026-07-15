import { describe, expect, test } from "bun:test";

import type { ReplayLevelFilePickerStatus, ReplayStatus } from "../activity.model";
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

  test("uses the exact replay command names and params", async () => {
    const calls: Array<{ method: string; params: unknown }> = [];
    const status = {
      OperationId: "op-1",
      RunId: "run-1",
      State: "preparing",
      ErrorCode: null,
      Message: null,
    } satisfies ReplayStatus;
    const pickerStatus = {
      OperationId: "picker-1",
      RunId: "run-1",
      State: "picking",
      LevelPath: null,
      ErrorCode: null,
      Message: null,
    } satisfies ReplayLevelFilePickerStatus;
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return method.includes("level-file") ? pickerStatus : status;
      },
    };
    const gateway = createActivityGateway(namespace as never);

    expect(await gateway.playReplay("run-1")).toBe(status);
    expect(await gateway.playReplay("run-1", "/levels/replay.adofai")).toBe(status);
    expect(await gateway.getReplayStatus()).toBe(status);
    expect(await gateway.startReplayLevelFilePicker("run-1")).toBe(pickerStatus);
    expect(await gateway.getReplayLevelFilePickerStatus("picker-1")).toBe(pickerStatus);
    expect(calls).toEqual([
      { method: "replay.play", params: { runId: "run-1" } },
      {
        method: "replay.play",
        params: { runId: "run-1", levelPath: "/levels/replay.adofai" },
      },
      { method: "replay.status.get", params: {} },
      { method: "replay.level-file.pick.start", params: { runId: "run-1" } },
      {
        method: "replay.level-file.pick.status",
        params: { operationId: "picker-1" },
      },
    ]);
  });
});
