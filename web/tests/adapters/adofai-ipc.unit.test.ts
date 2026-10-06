import { describe, expect, test } from "bun:test";
import { adaptChannel, createLazyAppChannels } from "@/adapters/adofai-ipc/connection";
import { IpcConnection } from "../../vendor/adofai-ipc/src";
import { scriptedChannels } from "../fixtures/local-message-peer";

describe("ADOFAI channel adapter", () => {
  test("retains the initial state until application subscribers attach and clears it on disconnect", () => {
    const connection = new IpcConnection();
    const channel = connection.namespace("tuf-replay");
    const peer = adaptChannel(channel);
    channel.receive({
      kind: "event",
      name: "replay.state.changed",
      id: "initial",
      payload: { state: "idle" },
    });
    const first: unknown[] = [];
    peer.on("replay.state.changed", (payload) => first.push(payload))();
    expect(first).toEqual([{ state: "idle" }]);
    channel.disconnected();
    const second: unknown[] = [];
    peer.on("replay.state.changed", (payload) => second.push(payload))();
    expect(second).toEqual([]);
  });
  test("captures a fast terminal bundle state even when another job publishes before subscription", () => {
    const connection = new IpcConnection();
    const channel = connection.namespace("tuf-replay");
    const peer = adaptChannel(channel);
    channel.receive({
      kind: "event",
      name: "render-bundle.state.changed",
      id: "owned",
      payload: { jobId: "owned", state: "completed" },
    });
    channel.receive({
      kind: "event",
      name: "render-bundle.state.changed",
      id: "other",
      payload: { jobId: "other", state: "preparing" },
    });
    const states: unknown[] = [];
    peer.on("render-bundle.state.changed", (payload) => states.push(payload))();
    expect(states).toEqual([
      { jobId: "owned", state: "completed" },
      { jobId: "other", state: "preparing" },
    ]);
    channel.disconnected();
    const stale: unknown[] = [];
    peer.on("render-bundle.state.changed", (payload) => stale.push(payload))();
    expect(stale).toEqual([]);
  });
  test("a failed initial connection can be retried through the same application ports", async () => {
    let attempts = 0;
    const channels = createLazyAppChannels(async () => {
      if (++attempts === 1) throw new Error("Start the game.");
      return scriptedChannels(() => ({}));
    });
    await expect(channels.namespace.whenReady()).rejects.toThrow("Start the game.");
    await channels.namespace.whenReady();
    expect(attempts).toBe(2);
  });
});
