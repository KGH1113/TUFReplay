import { describe, expect, test } from "bun:test";

import { createMockApi } from "@/mocks/create-mock-api";

describe("activity mock", () => {
  test("global camera retention refreshes summaries and preserves individual permanent clips", async () => {
    const api = createMockApi();
    const level = (await api.activity.listAllAppSessions())[0]?.levelSessions[0];
    if (!level || !api.webcam) throw new Error("mock camera is missing");
    const list = () =>
      api.activity.listLogicalLevelRuns(level.logicalLevelId, [level.appSessionId]);
    const cameras = (await list()).filter((item) => item.hasCameraRecording);
    const kept = cameras[0],
      temporary = cameras[1];
    if (!kept || !temporary) throw new Error("mock recordings are missing");
    const updates: number[] = [];
    const off = api.events.on("activity.changed", (event) => updates.push(event.revision));
    await api.run.keepCameraRecording(kept.id);
    await api.webcam.updateSettings({ retentionDays: 0 });
    expect((await list()).find((item) => item.id === temporary.id)).toMatchObject({
      cameraRecordingPermanent: true,
      cameraRecordingExpiresAtUtc: null,
    });
    await api.webcam.updateSettings({ retentionDays: 1 });
    const runs = await list();
    expect(runs.find((item) => item.id === kept.id)?.cameraRecordingPermanent).toBe(true);
    expect(runs.find((item) => item.id === temporary.id)).toMatchObject({
      cameraRecordingPermanent: false,
    });
    const expiry = runs.find((item) => item.id === temporary.id)?.cameraRecordingExpiresAtUtc;
    expect(Date.parse(expiry ?? "") - Date.now()).toBeGreaterThan(23 * 3_600_000);
    expect(updates).toEqual([1, 2]);
    off();
  });
  test("camera retention and deletion leave audio and run history intact", async () => {
    const api = createMockApi();
    const level = (await api.activity.listAllAppSessions())[0]?.levelSessions[0];
    if (!level) throw new Error("mock level is missing");
    const list = () =>
      api.activity.listLogicalLevelRuns(level.logicalLevelId, [level.appSessionId]);
    const originalRuns = await list();
    const run = originalRuns.find((item) => item.hasCameraRecording && item.hasMicrophoneRecording);
    if (!run) throw new Error("mock camera and microphone recordings are missing");
    await api.run.keepCameraRecording(run.id);
    expect((await list()).find((item) => item.id === run.id)).toMatchObject({
      cameraRecordingPermanent: true,
      cameraRecordingExpiresAtUtc: null,
      hasMicrophoneRecording: true,
      microphoneRecordingPermanent: false,
    });
    expect(run.cameraRecordingPermanent).toBe(false);
    expect(await api.run.deleteCameraRecording(run.id)).toEqual({ runId: run.id, changed: true });
    expect(await api.run.deleteCameraRecording(run.id)).toEqual({ runId: run.id, changed: false });
    const finalRuns = await list();
    expect(finalRuns.length).toBe(originalRuns.length);
    expect(finalRuns.find((item) => item.id === run.id)).toMatchObject({
      hasCameraRecording: false,
      cameraRecordingBytes: 0,
      cameraRecordingPermanent: false,
      cameraRecordingExpiresAtUtc: null,
      hasMicrophoneRecording: true,
      microphoneRecordingBytes: run.microphoneRecordingBytes,
      replayPlayable: true,
    });
    await expect(api.run.keepCameraRecording(run.id)).rejects.toMatchObject({
      code: "camera_recording_not_found",
    });
    await expect(api.run.prepareCameraRecordingDownload(run.id)).rejects.toMatchObject({
      code: "camera_recording_not_found",
    });
    await expect(api.run.prepareCameraRecordingDownload("unknown-run")).rejects.toMatchObject({
      code: "run_not_found",
    });
  });
  test("serves the downloaded TUF levels, including a long practice day, with charts and runs", async () => {
    const api = createMockApi();
    expect(await api.activity.getLegacyReplayStatus()).toEqual({ hasLegacyReplays: true });
    const sessions = await api.activity.listAllAppSessions();
    const levels = sessions.flatMap((session) => session.levelSessions);

    expect(levels.map((level) => level.tufLevelId).sort((a, b) => Number(a) - Number(b))).toEqual([
      5, 303, 871, 871,
    ]);

    let noFailRuns = 0;
    for (const level of levels) {
      const chart = await api.activity.getLogicalLevelChart(level.logicalLevelId);
      const runs = await api.activity.listLogicalLevelRuns(level.logicalLevelId, [
        level.appSessionId,
      ]);
      expect(chart.levelText).toContain('"settings"');
      expect(chart.floorCount).toBeGreaterThan(0);
      expect(runs.length).toBeGreaterThan(0);
      expect(runs.some((run) => run.startTile > 0)).toBe(true);
      noFailRuns += runs.filter((run) => run.noFailMode).length;
    }
    expect(noFailRuns).toBe(2);
  });

  test("deletes microphone recordings without removing their runs", async () => {
    const api = createMockApi();
    const sessions = await api.activity.listAllAppSessions();
    const level = sessions[0]?.levelSessions[0];
    if (!level) throw new Error("mock level is missing");
    const runs = await api.activity.listLogicalLevelRuns(level.logicalLevelId, [
      level.appSessionId,
    ]);
    const recordedRun = runs.find((run) => run.hasMicrophoneRecording);
    if (!recordedRun) throw new Error("mock microphone recording is missing");

    expect(await api.run.keepMicrophoneRecording(recordedRun.id)).toEqual({
      runId: recordedRun.id,
      changed: true,
    });
    expect(await api.run.deleteMicrophoneRecording(recordedRun.id)).toEqual({
      runId: recordedRun.id,
      changed: true,
    });
    expect(await api.run.deleteMicrophoneRecording(recordedRun.id)).toEqual({
      runId: recordedRun.id,
      changed: false,
    });
    const updatedRuns = await api.activity.listLogicalLevelRuns(level.logicalLevelId, [
      level.appSessionId,
    ]);
    expect(updatedRuns.find((run) => run.id === recordedRun.id)).toMatchObject({
      hasMicrophoneRecording: false,
      microphoneRecordingBytes: 0,
      microphoneDurationSeconds: null,
    });
  });

  test("locked runs survive single and day deletes and keep their recordings", async () => {
    const api = createMockApi();
    const sessions = await api.activity.listAllAppSessions();
    const app = sessions.find((item) => item.endedAtUtc !== null);
    const level = app?.levelSessions[0];
    if (!app || !level) throw new Error("mock closed session is missing");
    const list = () => api.activity.listLogicalLevelRuns(level.logicalLevelId, [app.id]);
    const [locked, clear] = [
      (await list()).find((item) => !isClear(item)),
      (await list()).find(isClear),
    ];
    if (!locked || !clear) throw new Error("mock runs are missing");

    await api.run.setRunLocked(locked.id, true);
    expect((await list()).find((item) => item.id === locked.id)).toMatchObject({ locked: true });
    await expect(api.run.deleteRun(locked.id)).rejects.toMatchObject({ code: "run_locked" });

    const result = await api.run.deleteAppSessionRuns([app.id], true);
    expect(result.keptLockedRunCount).toBe(
      app.levelSessions.reduce((sum, visit) => sum + visit.clearRunCount, 0) + 1,
    );
    const remaining = await list();
    expect(remaining.map((item) => item.id).sort()).toEqual([clear.id, locked.id].sort());
    expect(remaining.every((item) => item.locked)).toBe(true);

    for (const visit of app.levelSessions)
      for (const item of await api.activity.listLogicalLevelRuns(visit.logicalLevelId, [app.id]))
        await api.run.setRunLocked(item.id, false);
    await api.run.deleteAppSessionRuns([app.id], false);
    const after = await api.activity.listAllAppSessions();
    expect(after.some((item) => item.id === app.id)).toBe(false);
  });
});

function isClear(run: {
  result: string;
  startTile: number;
  noFailMode: boolean;
  judgmentCounts: { Miss: number; Overload: number };
}) {
  const result = run.result.toLowerCase();
  return (
    (result === "cleared" || result === "completed") &&
    run.startTile === 0 &&
    (!run.noFailMode || (run.judgmentCounts.Miss === 0 && run.judgmentCounts.Overload === 0))
  );
}
