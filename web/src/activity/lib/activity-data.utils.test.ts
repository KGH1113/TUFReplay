import { describe, expect, test } from "bun:test";

import type { ActivityAppSession, ActivityRun } from "../activity.model";
import { aggregateRunMarkers, groupSessionsByDay } from "./activity-data.utils";

const session = (id: string, started: string): ActivityAppSession => ({ Id: id, StartedAtUtc: started, EndedAtUtc: null, RecorderTimeZoneId: "Asia/Seoul", RecorderUtcOffsetMinutes: 540, LevelSessions: [] });
const run = (id: string, start: number, last: number, result = "failed"): ActivityRun => ({ Id: id, AppSessionId: "a", LevelSessionId: "l", TufLevelId: 1, RunIndex: 1, StartedAtUtc: "2026-01-01T00:00:00Z", EndedAtUtc: null, StartTile: start, LastTile: last, Result: result, NoFailMode: false, GameplayStartSongPosition: null, LevelPitchPercent: 100, EffectivePitch: 1, InputCount: 0, HitContextCount: 0, Meta: null });

describe("activity data", () => {
  test("groups every nested session under the app-session start day in the selected timezone", () => {
    const sessions = [session("late", "2026-01-01T23:30:00Z"), session("early", "2026-01-01T01:00:00Z")];
    expect(groupSessionsByDay(sessions, "Asia/Seoul").map((day) => [day.date, day.appSessions.map((item) => item.Id)])).toEqual([
      ["2026-01-02", ["late"]], ["2026-01-01", ["early"]],
    ]);
  });

  test("aggregates markers by StartTile with the exact marker fields", () => {
    expect(aggregateRunMarkers([run("a", 12, 20), run("b", 12, 30, "cleared")])).toEqual([{ id: "floor-12", floorIndex: 12, count: 2, clearCount: 1, bestLastFloorIndex: 30 }]);
  });
});
