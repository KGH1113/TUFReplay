import { describe, expect, test } from "bun:test";
import { createDownloadsApi } from "@/api/downloads/create-downloads-api";
import { createDownloadsApiMock } from "@/mocks/downloads/create-downloads-api-mock";
import { scriptedChannels } from "../../fixtures/local-message-peer";

const state = () => ({
  Renderer: {
    Status: "missing",
    DownloadedBytes: 0,
    TotalBytes: null,
    InstallationDirectory: "Mods/TUFReplay-Renderer",
  },
  Ffmpeg: {
    Status: "awaiting-consent",
    DownloadedBytes: 0,
    TotalBytes: null,
    InstallationDirectory: "Mods/TUFReplay/FFmpeg/windows-x64",
  },
  CameraNeedsFfmpeg: true,
});
const clients = scriptedChannels;
describe("download center IPC contract", () => {
  test("reads status through TUFReplay alone and keeps request separate from explicit consent", async () => {
    const calls: string[] = [];
    const api = createDownloadsApi(
      clients((method) => {
        calls.push(method);
        return method.startsWith("media.ffmpeg.") ? state().Ffmpeg : state();
      }),
    );
    await api.getStatus();
    await api.act("renderer", "request");
    expect(calls).toEqual(["downloads.state.read", "downloads.renderer.request"]);
    await api.act("renderer", "confirm");
    await api.act("ffmpeg", "request");
    await api.act("ffmpeg", "confirm");
    await api.act("ffmpeg", "cancel");
    await api.cancelPendingFfmpeg();
    expect(calls).toEqual([
      "downloads.state.read",
      "downloads.renderer.request",
      "downloads.renderer.confirm",
      "downloads.ffmpeg.request",
      "downloads.ffmpeg.confirm",
      "downloads.ffmpeg.cancel",
      "media.ffmpeg.release",
    ]);
  });
  test("rejects malformed installation states and retains precise failures", async () => {
    await expect(
      createDownloadsApi(
        clients(() => ({ ...state(), Renderer: { ...state().Renderer, Status: "success-ish" } })),
      ).getStatus(),
    ).rejects.toMatchObject({ kind: "validation" });
    await expect(
      createDownloadsApi(
        clients(() => ({ error: { code: "method_not_found", message: "Update TUFReplay" } })),
      ).getStatus(),
    ).rejects.toMatchObject({ code: "method_not_found" });
  });
  test("mock consent and cancellation do not install components or mutate returned snapshots", async () => {
    const api = createDownloadsApiMock();
    const initial = await api.getStatus();
    initial.Ffmpeg.Status = "ready";
    expect((await api.getStatus()).Ffmpeg.Status).toBe("missing");
    expect((await api.act("ffmpeg", "request")).Ffmpeg.Status).toBe("awaiting-consent");
    expect((await api.act("ffmpeg", "cancel")).Ffmpeg.Status).toBe("cancelled");
  });
});
