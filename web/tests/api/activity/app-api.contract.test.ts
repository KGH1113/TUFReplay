import { describe, expect, test } from "bun:test";
import { ReceivedFile } from "adofai-ipc";

import { createActivityApi as createActivityApiWithDownloads } from "@/api/activity/create-activity-api";
import { createHealthApi, SUPPORTED_PROTOCOL_VERSION } from "@/api/health/create-health-api";
import { createRunApi } from "@/api/run/create-run-api";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import { ApiError } from "@/shared/errors/api-error";
import { scriptedChannels } from "../../fixtures/local-message-peer";

type Call = { method: string; params: unknown };

const clientsWith = scriptedChannels;

const createActivityApi = (clients: LocalAppChannels) =>
  createActivityApiWithDownloads(clients, {
    readText: async () => {
      throw new Error("This test should not download chart bytes.");
    },
  });

function validAppSession() {
  return {
    Id: "app-1",
    StartedAtUtc: "2026-07-12T09:08:00.000Z",
    EndedAtUtc: null,
    RecorderTimeZoneId: "Asia/Seoul",
    RecorderUtcOffsetMinutes: 540,
    LevelSessions: [],
  };
}

describe("layered AppApi contract", () => {
  test("loads large chart text from the typed file while keeping control data small", async () => {
    const calls: Call[] = [];
    const signal = new AbortController().signal;
    const levelText = `{"comment":"${"한🙂".repeat(400_000)}"}`;
    const bytes = new TextEncoder().encode(levelText);
    const file = new ReceivedFile(
      "level.adofai",
      "application/json",
      bytes.length,
      () =>
        new ReadableStream({
          start(controller) {
            controller.enqueue(bytes);
            controller.close();
          },
        }),
    );
    const chart = { file, levelSessionId: "level-1", floorCount: 4896 };
    const api = createActivityApiWithDownloads(
      clientsWith((method, params) => {
        calls.push({ method, params });
        return chart;
      }),
      {
        async readText(received, options) {
          expect(received).toBe(file);
          expect(options?.signal).toBe(signal);
          return file.text(options);
        },
      },
    );
    expect(bytes.length).toBeGreaterThan(2 * 1024 * 1024);
    expect(await api.getLogicalLevelChart("level-1", { signal })).toEqual({
      levelSessionId: "level-1",
      levelText,
      floorCount: 4896,
    });
    expect(file.used).toBe(true);
    expect(calls).toEqual([{ method: "readLogicalLevelChart", params: { id: "level-1" } }]);
  });

  test("preserves domain failures before a file can be consumed", async () => {
    let downloads = 0;
    const missing = createActivityApiWithDownloads(
      clientsWith(() => ({
        error: { code: "level_file_missing", message: "Choose the chart again." },
      })),
      {
        async readText() {
          downloads++;
          return "{}";
        },
      },
    );
    await expect(missing.getLogicalLevelChart("level-1")).rejects.toMatchObject({
      kind: "domain",
      code: "level_file_missing",
    });
    expect(downloads).toBe(0);
  });

  test("validates health and maps its wire payload", async () => {
    const api = createHealthApi(
      clientsWith(() => ({
        Ok: true,
        Mod: "TUFReplay",
        ModVersion: "0.2.0-beta.1",
        ProtocolVersion: SUPPORTED_PROTOCOL_VERSION,
        ServerVersion: 1,
        ReplayEngineId: "tufreplay.replay.v2",
        ReplayFormatVersion: 1,
      })),
    );

    expect(await api.get()).toEqual({
      ok: true,
      mod: "TUFReplay",
      modVersion: "0.2.0-beta.1",
      protocolVersion: SUPPORTED_PROTOCOL_VERSION,
      serverVersion: 1,
      replayEngineId: "tufreplay.replay.v2",
      replayFormatVersion: 1,
      buildFlavor: "standard",
      autoSubmissionProtocolVersion: 0,
    });
  });

  test("classifies protocol mismatches and malformed payloads", async () => {
    const mismatch = createHealthApi(
      clientsWith(() => ({
        Ok: true,
        Mod: "TUFReplay",
        ModVersion: "0.2.0",
        ProtocolVersion: SUPPORTED_PROTOCOL_VERSION + 1,
        ServerVersion: 1,
        ReplayEngineId: "tufreplay.replay.v2",
        ReplayFormatVersion: 1,
      })),
    );
    const malformed = createHealthApi(clientsWith(() => ({ ProtocolVersion: "6" })));

    expect(mismatch.get()).rejects.toMatchObject({ kind: "protocol", code: "protocol_mismatch" });
    expect(malformed.get()).rejects.toMatchObject({ kind: "validation", code: "invalid_response" });
  });

  test("uses the bounded activity command and exposes camelCase models", async () => {
    const calls: Call[] = [];
    const api = createActivityApi(
      clientsWith((method, params) => {
        calls.push({ method, params });
        return [validAppSession()];
      }),
    );

    expect(await api.listAppSessions(40, 20)).toEqual([
      {
        id: "app-1",
        startedAtUtc: "2026-07-12T09:08:00.000Z",
        endedAtUtc: null,
        recorderTimeZoneId: "Asia/Seoul",
        recorderUtcOffsetMinutes: 540,
        levelSessions: [],
      },
    ]);
    expect(calls).toEqual([{ method: "listAppSessions", params: { offset: 40, limit: 20 } }]);
  });

  test("loads and validates the legacy replay status", async () => {
    const calls: Call[] = [];
    const api = createActivityApi(
      clientsWith((method, params) => {
        calls.push({ method, params });
        return { HasLegacyReplays: true };
      }),
    );

    expect(await api.getLegacyReplayStatus()).toEqual({ hasLegacyReplays: true });
    expect(calls).toEqual([{ method: "readLegacyReplayStatus", params: {} }]);

    const malformed = createActivityApi(clientsWith(() => ({ HasLegacyReplays: "yes" })));
    expect(malformed.getLegacyReplayStatus()).rejects.toMatchObject({ kind: "validation" });
  });

  test("rejects missing fields and invalid enum-like wire data at the API boundary", async () => {
    const api = createActivityApi(
      clientsWith(() => [{ ...validAppSession(), RecorderUtcOffsetMinutes: "540" }]),
    );

    expect(api.listAppSessions(0, 20)).rejects.toMatchObject({ kind: "validation" });
  });

  test("preserves domain and connection error classifications", async () => {
    const domainApi = createActivityApi(
      clientsWith(() => ({ error: { code: "not_found", message: "Session is missing" } })),
    );
    const connectionApi = createActivityApi(
      clientsWith(() => {
        throw new TypeError("network failed");
      }),
    );

    expect(domainApi.listAppSessions(0, 20)).rejects.toMatchObject({
      kind: "domain",
      code: "not_found",
      message: "Session is missing",
    });
    expect(connectionApi.listAppSessions(0, 20)).rejects.toBeInstanceOf(ApiError);
    expect(connectionApi.listAppSessions(0, 20)).rejects.toMatchObject({
      kind: "connection",
      code: "ipc_unavailable",
    });
  });

  test("run mutations preserve exact IPC method names and params", async () => {
    const calls: Call[] = [];
    const api = createRunApi(
      clientsWith((method, params) => {
        calls.push({ method, params });
        if (method === "retainMicrophoneRecording" || method === "retainCameraRecording") {
          return { RunId: "run-7", Permanent: true };
        }
        return { RunId: "run-7", Deleted: true };
      }),
    );

    expect(await api.deleteRun("run-7")).toEqual({ runId: "run-7", changed: true });
    expect(await api.deleteMicrophoneRecording("run-7")).toEqual({
      runId: "run-7",
      changed: true,
    });
    expect(await api.keepMicrophoneRecording("run-7")).toEqual({
      runId: "run-7",
      changed: true,
    });
    expect(await api.deleteCameraRecording("run-7")).toEqual({ runId: "run-7", changed: true });
    expect(await api.keepCameraRecording("run-7")).toEqual({ runId: "run-7", changed: true });
    expect(calls).toEqual([
      { method: "removeRun", params: { runId: "run-7" } },
      { method: "removeMicrophoneRecording", params: { runId: "run-7" } },
      { method: "retainMicrophoneRecording", params: { runId: "run-7" } },
      { method: "removeCameraRecording", params: { runId: "run-7" } },
      { method: "retainCameraRecording", params: { runId: "run-7" } },
    ]);
  });

  test("hands typed recordings to the native download API without reading bytes", async () => {
    const calls: Call[] = [];
    let downloads = 0;
    const received = {
      download: async () => {
        downloads++;
      },
    };
    const api = createRunApi(
      clientsWith((method, params) => {
        calls.push({ method, params });
        return received;
      }),
    );
    await (await api.prepareMicrophoneRecordingDownload("run-7")).download();
    await (await api.prepareCameraRecordingDownload("run-7")).download();
    expect(downloads).toBe(2);
    expect(calls).toEqual([
      { method: "exportMicrophoneRecording", params: { runId: "run-7" } },
      { method: "exportCameraRecording", params: { runId: "run-7" } },
    ]);
  });
});
