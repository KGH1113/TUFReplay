import { describe, expect, test } from "bun:test";

import type {
  ActivityAppSession,
  ActivityLevelSessionOverview,
  ActivityRun,
} from "../activity.model";
import { aggregateRunMarkers, groupSessionsByDay, isClearRun } from "./activity-data.utils";

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
  levelGroupId = "group-7",
  clearRunCount = 0,
  noFailRunCount = 0,
): ActivityLevelSessionOverview => ({
  Id: id,
  LogicalLevelId: logicalLevelId,
  LevelGroupId: levelGroupId,
  AppSessionId: appSessionId,
  TufLevelId: 7,
  Song: "Same level",
  Author: "Creator",
  Artist: "Artist",
  OpenedAtUtc: openedAtUtc,
  ClosedAtUtc: null,
  FloorCount: 100,
  RunCount: runCount,
  ClearRunCount: clearRunCount,
  NoFailRunCount: noFailRunCount,
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
      { id: "floor-12", floorIndex: 12, count: 2, clearCount: 0, bestLastFloorIndex: 30 },
    ]);
  });

  test("counts a clear only for a full normal-mode run from tile zero", () => {
    const qualified = run("qualified", 0, 100, "cleared");
    const checkpoint = run("checkpoint", 40, 100, "cleared");
    const noFail = { ...run("no-fail", 0, 100, "completed"), NoFailMode: true };
    const roundedOnly = run("rounded", 0, 99, "failed");

    expect([qualified, checkpoint, noFail, roundedOnly].map(isClearRun)).toEqual([
      true,
      false,
      false,
      false,
    ]);
    expect(aggregateRunMarkers([qualified, checkpoint, noFail, roundedOnly])).toEqual([
      { id: "floor-0", floorIndex: 0, count: 3, clearCount: 1, bestLastFloorIndex: 100 },
      { id: "floor-40", floorIndex: 40, count: 1, clearCount: 0, bestLastFloorIndex: 100 },
    ]);
  });

  test("deduplicates the same logical level while preserving visits across app sessions", () => {
    const first = session("app-a", "2026-01-01T01:00:00Z");
    const second = session("app-b", "2026-01-01T02:00:00Z");
    first.LevelSessions = [visit("visit-a", "logical-7", first.Id, first.StartedAtUtc, 2)];
    second.LevelSessions = [visit("visit-b", "logical-7", second.Id, second.StartedAtUtc, 3)];
    const [day] = groupSessionsByDay([first, second], "UTC");
    expect(day.levelSessions).toHaveLength(1);
    expect(day.levelSessions[0]).toMatchObject({
      Id: "logical-7",
      VisitCount: 2,
      RunCount: 5,
      HiddenRunCount: 0,
    });
    expect(day.runCount).toBe(5);
  });

  test("shows only the latest revision and counts runs from earlier revisions as hidden", () => {
    const before = session("app-before", "2026-01-01T01:00:00Z");
    const after = session("app-after", "2026-01-01T02:00:00Z");
    before.LevelSessions = [
      visit("visit-before", "revision-before", before.Id, before.StartedAtUtc, 3, "group-7", 2, 1),
    ];
    after.LevelSessions = [
      visit("visit-after", "revision-after", after.Id, after.StartedAtUtc, 5, "group-7", 4, 2),
    ];

    const [day] = groupSessionsByDay([before, after], "UTC");
    expect(day.levelSessions).toHaveLength(1);
    expect(day.levelSessions[0]).toMatchObject({
      Id: "revision-after",
      RunCount: 5,
      ClearRunCount: 4,
      NoFailRunCount: 2,
      HiddenRunCount: 3,
    });
    expect(day.runCount).toBe(5);
    expect(day.clearRunCount).toBe(4);
  });

  test("keeps an earlier day's counts but blocks its outdated revision", () => {
    const oldDay = session("app-old", "2026-08-14T01:00:00Z");
    const beforeEdit = session("app-before-edit", "2026-08-16T01:00:00Z");
    const afterEdit = session("app-after-edit", "2026-08-16T02:00:00Z");
    oldDay.LevelSessions = [
      visit("visit-old", "revision-a", oldDay.Id, oldDay.StartedAtUtc, 4, "group-7", 2),
    ];
    beforeEdit.LevelSessions = [
      visit(
        "visit-before-edit",
        "revision-a",
        beforeEdit.Id,
        beforeEdit.StartedAtUtc,
        3,
        "group-7",
        1,
      ),
    ];
    afterEdit.LevelSessions = [
      visit(
        "visit-after-edit",
        "revision-b",
        afterEdit.Id,
        afterEdit.StartedAtUtc,
        5,
        "group-7",
        4,
      ),
    ];

    const [latestDay, earlierDay] = groupSessionsByDay([oldDay, beforeEdit, afterEdit], "UTC");
    expect(latestDay.levelSessions[0]).toMatchObject({
      Id: "revision-b",
      CanOpen: true,
      RunCount: 5,
      VisibleRunCount: 5,
      HiddenRunCount: 3,
    });
    expect(latestDay.runCount).toBe(5);
    expect(earlierDay.levelSessions[0]).toMatchObject({
      Id: "revision-a",
      CanOpen: false,
      RunCount: 4,
      VisibleRunCount: 0,
      HiddenRunCount: 4,
    });
    expect(earlierDay).toMatchObject({ hasOpenableLevels: false, runCount: 4, clearRunCount: 2 });
  });

  test("uses every visit of the revision played most recently after an A-B-A sequence", () => {
    const firstA = session("app-a1", "2026-01-01T01:00:00Z");
    const revisionB = session("app-b", "2026-01-01T02:00:00Z");
    const latestA = session("app-a2", "2026-01-01T03:00:00Z");
    firstA.LevelSessions = [visit("visit-a1", "revision-a", firstA.Id, firstA.StartedAtUtc, 2)];
    revisionB.LevelSessions = [
      visit("visit-b", "revision-b", revisionB.Id, revisionB.StartedAtUtc, 4),
    ];
    latestA.LevelSessions = [visit("visit-a2", "revision-a", latestA.Id, latestA.StartedAtUtc, 3)];

    const [day] = groupSessionsByDay([firstA, revisionB, latestA], "UTC");
    expect(day.levelSessions[0]).toMatchObject({
      Id: "revision-a",
      VisitCount: 2,
      RunCount: 5,
      HiddenRunCount: 4,
    });
  });

  test("keeps the same logical level's summary scoped to each day", () => {
    const first = session("app-a", "2026-01-01T01:00:00Z");
    const second = session("app-b", "2026-01-02T01:00:00Z");
    first.LevelSessions = [visit("visit-a", "logical-7", first.Id, first.StartedAtUtc, 2)];
    second.LevelSessions = [visit("visit-b", "logical-7", second.Id, second.StartedAtUtc, 5)];

    const days = groupSessionsByDay([first, second], "UTC");
    expect(days.map((day) => [day.date, day.levelSessions[0].RunCount])).toEqual([
      ["2026-01-02", 5],
      ["2026-01-01", 2],
    ]);
    expect(days.map((day) => day.levelSessions[0].HiddenRunCount)).toEqual([0, 0]);
  });
});
