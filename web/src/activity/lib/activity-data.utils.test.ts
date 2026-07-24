import { describe, expect, test } from "bun:test";

import type {
  ActivityAppSession,
  ActivityLevelSessionOverview,
  ActivityRun,
} from "../activity.model";
import { aggregateRunMarkers, groupSessionsByDay } from "./activity-data.utils";

const session = (id: string, started: string): ActivityAppSession => ({
  Id: id,
  StartedAtUtc: started,
  EndedAtUtc: null,
  RecorderTimeZoneId: "Asia/Seoul",
  RecorderUtcOffsetMinutes: 540,
  LevelSessions: [],
});
const visit = (
  id: string,
  logicalLevelId: string,
  appSessionId: string,
  openedAtUtc: string,
  runCount: number,
): ActivityLevelSessionOverview => ({
  Id: id,
  LogicalLevelId: logicalLevelId,
  AppSessionId: appSessionId,
  TufLevelId: 7,
  Song: "Same level",
  Author: "Creator",
  Artist: "Artist",
  OpenedAtUtc: openedAtUtc,
  ClosedAtUtc: null,
  FloorCount: 100,
  RunCount: runCount,
  ClearRunCount: 0,
  NoFailRunCount: 0,
  FirstStartTile: 0,
  LastStartTile: 0,
  ChartAvailable: true,
});
const run = (id: string, start: number, last: number, result = "failed"): ActivityRun => ({
  Id: id,
  LevelSessionId: "l",
  TufLevelId: 1,
  RunIndex: 1,
  StartedAtUtc: "2026-01-01T00:00:00Z",
  EndedAtUtc: null,
  StartTile: start,
  LastTile: last,
  Result: result,
  NoFailMode: false,
  GameplayStartSongPosition: null,
  LevelPitchPercent: 100,
  EffectivePitch: 1,
  XAccuracy: 0.95,
  JudgmentDifficulty: "Strict",
  JudgmentCounts: {
    Overload: 0,
    TooEarly: 0,
    Early: 0,
    EarlyPerfect: 0,
    Perfect: 0,
    LatePerfect: 0,
    Late: 0,
    TooLate: 0,
    Miss: 0,
  },
  InputCount: 0,
  HitContextCount: 0,
  FloorCount: 100,
  InputBytes: 0,
  HitContextBytes: 0,
  HasMicrophoneRecording: false,
  MicrophoneRecordingBytes: 0,
  MicrophoneDurationSeconds: null,
  MicrophoneSampleRate: null,
  MicrophoneChannels: null,
  MicrophoneRecordingPermanent: false,
  MicrophoneRecordingExpiresAtUtc: null,
});

describe("activity data", () => {
  test("groups every nested session under the app-session start day in the selected timezone", () => {
    const sessions = [
      session("late", "2026-01-01T23:30:00Z"),
      session("early", "2026-01-01T01:00:00Z"),
    ];
    expect(
      groupSessionsByDay(sessions, "Asia/Seoul").map((day) => [
        day.date,
        day.appSessions.map((item) => item.Id),
      ]),
    ).toEqual([
      ["2026-01-02", ["late"]],
      ["2026-01-01", ["early"]],
    ]);
  });

  test("aggregates markers by StartTile with the exact marker fields", () => {
    expect(aggregateRunMarkers([run("a", 12, 20), run("b", 12, 30, "cleared")])).toEqual([
      { id: "floor-12", floorIndex: 12, count: 2, clearCount: 1, bestLastFloorIndex: 30 },
    ]);
  });

  test("deduplicates the same logical level while preserving visits across app sessions", () => {
    const first = session("app-a", "2026-01-01T01:00:00Z");
    const second = session("app-b", "2026-01-01T02:00:00Z");
    first.LevelSessions = [visit("visit-a", "logical-7", first.Id, first.StartedAtUtc, 2)];
    second.LevelSessions = [visit("visit-b", "logical-7", second.Id, second.StartedAtUtc, 3)];
    const [day] = groupSessionsByDay([first, second], "UTC");
    expect(day.levelSessions).toHaveLength(1);
    expect(day.levelSessions[0]).toMatchObject({ Id: "logical-7", VisitCount: 2, RunCount: 5 });
    expect(day.runCount).toBe(5);
  });
});
