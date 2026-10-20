import { describe, expect, test } from "bun:test";
import { createLazyAppChannels, createRecorderSnapshots } from "@/adapters/adofai-ipc/connection";
import { scriptedChannels } from "../fixtures/local-message-peer";

describe("typed ADOFAI recorder adapter", () => {
  test("retains initial snapshots for late subscribers and clears disconnected state", () => {
    const snapshots = createRecorderSnapshots();
    snapshots.handlers.activityChanged({ value: { revision: 3, runId: null } });
    const first: unknown[] = [];
    snapshots.on("activityChanged", (value) => first.push(value))();
    expect(first).toEqual([{ revision: 3, runId: null }]);
    snapshots.clear();
    const stale: unknown[] = [];
    snapshots.on("activityChanged", (value) => stale.push(value))();
    expect(stale).toEqual([]);
    snapshots.handlers.activityChanged({ value: { revision: 4, runId: "new-session" } });
    snapshots.on("activityChanged", (value) => stale.push(value))();
    expect(stale).toEqual([{ revision: 4, runId: "new-session" }]);
  });
  test("retains a terminal bundle snapshot independently from other jobs", () => {
    const snapshots = createRecorderSnapshots();
    const base = {
      runId: "run",
      progress: 1,
      manifestPath: null,
      errorCode: null,
      errorMessage: null,
      errorDetails: null,
    };
    snapshots.handlers.renderBundleStateChanged({
      value: { ...base, jobId: "owned", state: "completed" },
    });
    snapshots.handlers.renderBundleStateChanged({
      value: { ...base, jobId: "other", state: "preparing" },
    });
    const ids: string[] = [];
    snapshots.on("renderBundleStateChanged", (value) => ids.push(value.jobId))();
    expect(ids).toEqual(["owned", "other"]);
  });
  test("a failed initial connection can be retried through the same ports", async () => {
    let attempts = 0;
    const channels = createLazyAppChannels(async () => {
      if (++attempts === 1) throw new Error("Start the game.");
      return scriptedChannels(() => ({}));
    });
    await expect(channels.recorder.whenReady()).rejects.toThrow("Start the game.");
    await channels.recorder.whenReady();
    expect(attempts).toBe(2);
  });
});
