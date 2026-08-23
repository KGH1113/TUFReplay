import { describe, expect, test } from "bun:test";

import { createActivityApi } from "@/api/activity/create-activity-api";
import { createHealthApi, SUPPORTED_PROTOCOL_VERSION } from "@/api/health/create-health-api";
import { createRunApi } from "@/api/run/create-run-api";
import type { AdofaiIpcClients } from "@/shared/clients/adofai-ipc-client";
import { ApiError } from "@/shared/errors/api-error";

type Call = { method: string; params: unknown };

function clientsWith(call: (method: string, params: unknown) => unknown): AdofaiIpcClients {
  const namespace = { call: async (method: string, params: unknown) => call(method, params) };
  return { namespace, pickerNamespace: namespace } as unknown as AdofaiIpcClients;
}

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
  test("validates health and maps its wire payload", async () => {
    const api = createHealthApi(
      clientsWith(() => ({
        Ok: true,
        Mod: "TUFReplay",
        ModVersion: "0.1.0",
        ProtocolVersion: SUPPORTED_PROTOCOL_VERSION,
        ServerVersion: 1,
      })),
    );

    expect(await api.get()).toEqual({
      ok: true,
      mod: "TUFReplay",
      modVersion: "0.1.0",
      protocolVersion: SUPPORTED_PROTOCOL_VERSION,
      serverVersion: 1,
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
    expect(calls).toEqual([
      { method: "activity.app-sessions.list", params: { offset: 40, limit: 20 } },
    ]);
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
    expect(connectionApi.listAppSessions(0, 20)).rejects.toMatchObject({ kind: "connection" });
  });

  test("run mutations preserve exact IPC method names and params", async () => {
    const calls: Call[] = [];
    const api = createRunApi(
      clientsWith((method, params) => {
        calls.push({ method, params });
        if (method === "microphone.recording.keep") {
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
    expect(calls).toEqual([
      { method: "activity.run.delete", params: { runId: "run-7" } },
      { method: "microphone.recording.delete", params: { runId: "run-7" } },
      { method: "microphone.recording.keep", params: { runId: "run-7" } },
    ]);
  });
});
