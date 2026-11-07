import { describe, expect, test } from "bun:test";
import type { AppEventMap } from "@/api/app-events";
import { createAppEventSource } from "@/application/event-source";
import { waitForAppState } from "@/application/wait-for-app-state";
import { createRenderApiMock } from "@/mocks/render/create-render-api-mock";

const renderState = (
  jobId: string,
  state: "exporting" | "completed",
): AppEventMap["render-state.changed"] => ({
  jobId,
  state,
  stage: state === "completed" ? null : "exporting_recording",
  progress: state === "completed" ? 1 : 0,
  outputFile: null,
  warnings: [],
  waitingForGameFocus: false,
  errorCode: null,
  errorMessage: null,
  errorDetails: null,
  outputDirectory: null,
  localOutputPath: null,
  canOpenOutput: false,
  canDownload: false,
});
describe("authoritative render state subscriptions", () => {
  test("replays a completed snapshot and ignores other jobs without issuing status queries", async () => {
    const source = createAppEventSource();
    const states: string[] = [];
    const wait = waitForAppState(
      source.events,
      "render-state.changed",
      (state) => state.jobId === "owned" && state.state === "completed",
      { onState: (state) => states.push(state.jobId) },
    );
    source.emit("render-state.changed", renderState("other", "completed"));
    source.emit("render-state.changed", renderState("owned", "completed"));
    expect((await wait).jobId).toBe("owned");
    source.emit("render-state.changed", renderState("later", "completed"));
    expect(states).toEqual(["other", "owned"]);
    expect(
      (
        await waitForAppState(
          source.events,
          "render-state.changed",
          (state) => state.jobId === "later",
        )
      ).state,
    ).toBe("completed");
  });
  test("disconnect ends a wait immediately and removes the listener", async () => {
    const source = createAppEventSource();
    let updates = 0;
    const wait = waitForAppState(source.events, "render-state.changed", () => false, {
      onState: () => updates++,
    });
    source.emit("connection.changed", "reconnecting");
    await expect(wait).rejects.toMatchObject({ kind: "connection", code: "ipc_unavailable" });
    source.emit("render-state.changed", renderState("owned", "completed"));
    expect(updates).toBe(0);
  });
  test("a renderer unload ends only its own waits while the gateway stays connected", async () => {
    const source = createAppEventSource();
    source.emit("connection.changed", "connected");
    const abort = new AbortController();
    const recorderWait = waitForAppState(source.events, "render-state.changed", () => false, {
      namespace: "recorder",
      signal: abort.signal,
    });
    const rendererWait = waitForAppState(source.events, "renderer.folder.changed", () => false, {
      namespace: "renderer",
    });
    source.emit("renderer.status.changed", "unavailable");
    await expect(rendererWait).rejects.toMatchObject({ code: "ipc_unavailable" });
    abort.abort();
    await expect(recorderWait).rejects.toMatchObject({ name: "AbortError" });
  });
  test("abort releases folder waits and does not apply late selection", async () => {
    const source = createAppEventSource();
    const abort = new AbortController();
    let updates = 0;
    const wait = waitForAppState(
      source.events,
      "renderer.folder.changed",
      (state) => !state.pending,
      { signal: abort.signal, onState: () => updates++ },
    );
    abort.abort();
    await expect(wait).rejects.toMatchObject({ name: "AbortError" });
    source.emit("renderer.folder.changed", {
      selectionId: "folder",
      pending: false,
      outputDirectory: "/tmp",
    });
    expect(updates).toBe(0);
  });
  test("settings probe completion is pushed and callback errors clean up", async () => {
    const source = createAppEventSource();
    const wait = waitForAppState(
      source.events,
      "renderer.settings.changed",
      (state) => state.system?.encoding.state !== "checking",
    );
    source.emit("renderer.settings.changed", {
      ...(await createRenderApiMock().getSettings()),
      outputDirectory: "/tmp",
    });
    expect((await wait).outputDirectory).toBe("/tmp");
    await expect(
      waitForAppState(source.events, "renderer.settings.changed", () => {
        throw new Error("decode");
      }),
    ).rejects.toThrow("decode");
  });
});
