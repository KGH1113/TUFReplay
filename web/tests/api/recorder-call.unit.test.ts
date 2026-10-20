import { describe, expect, test } from "bun:test";
import { callRecorder } from "@/api/recorder-call";
import { scriptedChannels } from "../fixtures/local-message-peer";

describe("typed recorder application calls", () => {
  test.each([
    "protocol_mismatch",
    "contract_mismatch",
    "feature_protocol_mismatch",
  ])("classifies %s as a compatibility error", async (code) => {
    const clients = scriptedChannels(() => {
      throw Object.assign(new Error("Update the mod and companion."), { code });
    });
    await expect(callRecorder(clients, (mod) => mod.readHealth())).rejects.toMatchObject({
      kind: "protocol",
      code,
    });
  });
  test.each([
    "TIMEOUT",
    "UNAVAILABLE",
  ])("keeps the SDK's %s readiness failure recoverable", async (code) => {
    const clients = scriptedChannels(() => {
      throw Object.assign(new Error("Reconnect to the game."), { code });
    });
    await expect(callRecorder(clients, (mod) => mod.readHealth())).rejects.toMatchObject({
      kind: "connection",
    });
  });
  test("an already cancelled preview does not request a new file", async () => {
    let calls = 0;
    const clients = scriptedChannels(() => {
      calls++;
      return null;
    });
    const controller = new AbortController();
    controller.abort(new DOMException("Preview closed", "AbortError"));
    await expect(
      callRecorder(clients, (mod) => mod.readWebcamPreview({ deviceId: null }), {
        signal: controller.signal,
      }),
    ).rejects.toMatchObject({ name: "AbortError" });
    expect(calls).toBe(0);
  });

  test("cancelling a wait rejects promptly without issuing another operation", async () => {
    let calls = 0;
    let finish: (value: unknown) => void = () => {};
    const clients = scriptedChannels(() => {
      calls++;
      return new Promise((resolve) => {
        finish = resolve;
      });
    });
    const controller = new AbortController();
    const pending = callRecorder(clients, (mod) => mod.readWebcamPreview({ deviceId: null }), {
      signal: controller.signal,
    });
    await Promise.resolve();
    controller.abort(new DOMException("Preview closed", "AbortError"));
    await expect(pending).rejects.toMatchObject({ name: "AbortError" });
    finish(null);
    expect(calls).toBe(1);
  });

  test("a timeout retains a user-facing connection error", async () => {
    const clients = scriptedChannels(() => new Promise(() => {}));
    await expect(
      callRecorder(clients, (mod) => mod.readHealth(), { timeoutMs: 5 }),
    ).rejects.toMatchObject({ kind: "connection", code: "ipc_timeout" });
  });
});
