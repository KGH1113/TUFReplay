import { describe, expect, test } from "bun:test";

import { createMockApi } from "@/mocks/create-mock-api";

describe("activity mock", () => {
  test("serves the three downloaded TUF levels with charts and runs", async () => {
    const api = createMockApi();
    const sessions = await api.activity.listAllAppSessions();
    const levels = sessions.flatMap((session) => session.levelSessions);

    expect(levels.map((level) => level.tufLevelId).sort((a, b) => Number(a) - Number(b))).toEqual([
      5, 303, 871,
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
    expect(noFailRuns).toBe(1);
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
});
