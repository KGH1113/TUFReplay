import { describe, expect, test } from "bun:test";
import {
  aggregateRunMarkers,
  groupSessionsByDay,
  isClearRun,
} from "@/models/activity/activity-data";
import type { ActivityRun, AppSession, LevelSession } from "@/models/activity/activity-model";

const session = (id: string, started: string): AppSession => ({
  id: id,
  startedAtUtc: started,
  endedAtUtc: null,
  recorderTimeZoneId: "Asia/Seoul",
  recorderUtcOffsetMinutes: 540,
  levelSessions: [],
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
): LevelSession => ({
  id: id,
  logicalLevelId: logicalLevelId,
  levelGroupId: levelGroupId,
  appSessionId: appSessionId,
  tufLevelId: 7,
  song: "Same level",
  author: "Creator",
  artist: "artist",
  openedAtUtc: openedAtUtc,
  closedAtUtc: null,
  floorCount: 100,
  runCount: runCount,
  clearRunCount: clearRunCount,
  noFailRunCount: noFailRunCount,
  firstStartTile: 0,
  lastStartTile: 0,
  chartAvailable: true,
});
const run = (id: string, start: number, last: number, result = "failed"): ActivityRun => ({
  id: id,
  levelSessionId: "l",
  tufLevelId: 1,
  runIndex: 1,
  startedAtUtc: "2026-01-01T00:00:00Z",
  endedAtUtc: null,
  startTile: start,
  lastTile: last,
  result: result,
  noFailMode: false,
  gameplayStartSongPosition: null,
  levelPitchPercent: 100,
  effectivePitch: 1,
  xAccuracy: 0.95,
  judgmentDifficulty: "Strict",
  judgmentSystem: "Legacy",
  judgmentCounts: {
    Overload: 0,
    TooEarly: 0,
    Early: 0,
    EarlyPerfect: 0,
    Perfect: 0,
    PerfectMinus: 0,
    XPerfect: 0,
    PerfectPlus: 0,
    LatePerfect: 0,
    Late: 0,
    TooLate: 0,
    Miss: 0,
  },
  inputCount: 0,
  hitContextCount: 0,
  floorCount: 100,
  inputBytes: 0,
  hitContextBytes: 0,
  hasMicrophoneRecording: false,
  microphoneRecordingBytes: 0,
  microphoneDurationSeconds: null,
  microphoneSampleRate: null,
  microphoneChannels: null,
  microphoneRecordingPermanent: false,
  microphoneRecordingExpiresAtUtc: null,
  submissionRunId: null,
  replayPlayable: true,
  replayUnavailableReason: null,
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
        day.appSessions.map((item) => item.id),
      ]),
    ).toEqual([
      ["2026-01-02", ["late"]],
      ["2026-01-01", ["early"]],
    ]);
  });

  test("aggregates markers by startTile with the exact marker fields", () => {
    expect(aggregateRunMarkers([run("a", 12, 20), run("b", 12, 30, "cleared")])).toEqual([
      { id: "floor-12", floorIndex: 12, count: 2, clearCount: 0, bestLastFloorIndex: 30 },
    ]);
  });

  test("counts a clear only for a full normal-mode run from tile zero", () => {
    const qualified = run("qualified", 0, 100, "cleared");
    const checkpoint = run("checkpoint", 40, 100, "cleared");
    const noFail = { ...run("no-fail", 0, 100, "completed"), noFailMode: true };
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
    first.levelSessions = [visit("visit-a", "logical-7", first.id, first.startedAtUtc, 2)];
    second.levelSessions = [visit("visit-b", "logical-7", second.id, second.startedAtUtc, 3)];
    const [day] = groupSessionsByDay([first, second], "UTC");
    expect(day.levelSessions).toHaveLength(1);
    expect(day.levelSessions[0]).toMatchObject({
      id: "logical-7",
      visitCount: 2,
      runCount: 5,
      hiddenRunCount: 0,
    });
    expect(day.runCount).toBe(5);
  });

  test("shows only the latest revision and counts runs from earlier revisions as hidden", () => {
    const before = session("app-before", "2026-01-01T01:00:00Z");
    const after = session("app-after", "2026-01-01T02:00:00Z");
    before.levelSessions = [
      visit("visit-before", "revision-before", before.id, before.startedAtUtc, 3, "group-7", 2, 1),
    ];
    after.levelSessions = [
      visit("visit-after", "revision-after", after.id, after.startedAtUtc, 5, "group-7", 4, 2),
    ];

    const [day] = groupSessionsByDay([before, after], "UTC");
    expect(day.levelSessions).toHaveLength(1);
    expect(day.levelSessions[0]).toMatchObject({
      id: "revision-after",
      runCount: 5,
      clearRunCount: 4,
      noFailRunCount: 2,
      hiddenRunCount: 3,
    });
    expect(day.runCount).toBe(5);
    expect(day.clearRunCount).toBe(4);
  });

  test("keeps an earlier day's counts but blocks its outdated revision", () => {
    const oldDay = session("app-old", "2026-08-14T01:00:00Z");
    const beforeEdit = session("app-before-edit", "2026-08-16T01:00:00Z");
    const afterEdit = session("app-after-edit", "2026-08-16T02:00:00Z");
    oldDay.levelSessions = [
      visit("visit-old", "revision-a", oldDay.id, oldDay.startedAtUtc, 4, "group-7", 2),
    ];
    beforeEdit.levelSessions = [
      visit(
        "visit-before-edit",
        "revision-a",
        beforeEdit.id,
        beforeEdit.startedAtUtc,
        3,
        "group-7",
        1,
      ),
    ];
    afterEdit.levelSessions = [
      visit(
        "visit-after-edit",
        "revision-b",
        afterEdit.id,
        afterEdit.startedAtUtc,
        5,
        "group-7",
        4,
      ),
    ];

    const [latestDay, earlierDay] = groupSessionsByDay([oldDay, beforeEdit, afterEdit], "UTC");
    expect(latestDay.levelSessions[0]).toMatchObject({
      id: "revision-b",
      canOpen: true,
      runCount: 5,
      visibleRunCount: 5,
      hiddenRunCount: 3,
    });
    expect(latestDay.runCount).toBe(5);
    expect(earlierDay.levelSessions[0]).toMatchObject({
      id: "revision-a",
      canOpen: false,
      runCount: 4,
      visibleRunCount: 0,
      hiddenRunCount: 4,
    });
    expect(earlierDay).toMatchObject({ hasOpenableLevels: false, runCount: 4, clearRunCount: 2 });
  });

  test("uses every visit of the revision played most recently after an A-B-A sequence", () => {
    const firstA = session("app-a1", "2026-01-01T01:00:00Z");
    const revisionB = session("app-b", "2026-01-01T02:00:00Z");
    const latestA = session("app-a2", "2026-01-01T03:00:00Z");
    firstA.levelSessions = [visit("visit-a1", "revision-a", firstA.id, firstA.startedAtUtc, 2)];
    revisionB.levelSessions = [
      visit("visit-b", "revision-b", revisionB.id, revisionB.startedAtUtc, 4),
    ];
    latestA.levelSessions = [visit("visit-a2", "revision-a", latestA.id, latestA.startedAtUtc, 3)];

    const [day] = groupSessionsByDay([firstA, revisionB, latestA], "UTC");
    expect(day.levelSessions[0]).toMatchObject({
      id: "revision-a",
      visitCount: 2,
      runCount: 5,
      hiddenRunCount: 4,
    });
  });

  test("keeps the same logical level's summary scoped to each day", () => {
    const first = session("app-a", "2026-01-01T01:00:00Z");
    const second = session("app-b", "2026-01-02T01:00:00Z");
    first.levelSessions = [visit("visit-a", "logical-7", first.id, first.startedAtUtc, 2)];
    second.levelSessions = [visit("visit-b", "logical-7", second.id, second.startedAtUtc, 5)];

    const days = groupSessionsByDay([first, second], "UTC");
    expect(days.map((day) => [day.date, day.levelSessions[0].runCount])).toEqual([
      ["2026-01-02", 5],
      ["2026-01-01", 2],
    ]);
    expect(days.map((day) => day.levelSessions[0].hiddenRunCount)).toEqual([0, 0]);
  });
});
