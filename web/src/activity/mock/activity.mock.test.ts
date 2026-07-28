import { describe, expect, test } from "bun:test";

import { createMockActivityGateway } from "./activity.mock";

describe("activity mock", () => {
  test("serves the three downloaded TUF levels with charts and runs", async () => {
    const gateway = createMockActivityGateway();
    const sessions = await gateway.listAllAppSessions();
    const levels = sessions.flatMap((session) => session.LevelSessions);

    expect(levels.map((level) => level.TufLevelId).sort((a, b) => Number(a) - Number(b))).toEqual([
      5, 303, 871,
    ]);

    let noFailRuns = 0;
    for (const level of levels) {
      const chart = await gateway.getChart(level.Id);
      const runs = await gateway.listAllRuns(level.Id);
      expect(chart.LevelText).toContain('"settings"');
      expect(chart.FloorCount).toBeGreaterThan(0);
      expect(runs.length).toBeGreaterThan(0);
      expect(runs.some((run) => run.StartTile > 0)).toBe(true);
      noFailRuns += runs.filter((run) => run.NoFailMode).length;
    }
    expect(noFailRuns).toBe(1);
  });

  test("deletes microphone recordings without removing their runs", async () => {
    const gateway = createMockActivityGateway();
    const sessions = await gateway.listAllAppSessions();
    const levelId = sessions[0]?.LevelSessions[0]?.Id;
    if (!levelId) throw new Error("mock level is missing");
    const runs = await gateway.listAllRuns(levelId);
    const recordedRun = runs.find((run) => run.HasMicrophoneRecording);
    if (!recordedRun) throw new Error("mock microphone recording is missing");

    expect(await gateway.keepMicrophoneRecording(recordedRun.Id)).toEqual({
      RunId: recordedRun.Id,
      Permanent: true,
    });
    expect(recordedRun).toMatchObject({
      MicrophoneRecordingPermanent: true,
      MicrophoneRecordingExpiresAtUtc: null,
    });
    expect(await gateway.deleteMicrophoneRecording(recordedRun.Id)).toEqual({
      RunId: recordedRun.Id,
      Deleted: true,
    });
    expect(await gateway.deleteMicrophoneRecording(recordedRun.Id)).toEqual({
      RunId: recordedRun.Id,
      Deleted: false,
    });
    const updatedRuns = await gateway.listAllRuns(levelId);
    expect(updatedRuns.find((run) => run.Id === recordedRun.Id)).toMatchObject({
      HasMicrophoneRecording: false,
      MicrophoneRecordingBytes: 0,
      MicrophoneDurationSeconds: null,
    });
  });
});
