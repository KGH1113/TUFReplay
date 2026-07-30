import { describe, expect, test } from "bun:test";

import type { ActivityAppSession } from "../activity.model";
import { ActivityProtocolMismatchError } from "../data/activity.gateway";
import { connectionStatusForError, mergeRecentAppSessions } from "./use-activity-data.hook";

function appSession(id: string, startedAtUtc: string, endedAtUtc: string | null = null) {
  return {
    Id: id,
    StartedAtUtc: startedAtUtc,
    EndedAtUtc: endedAtUtc,
    RecorderTimeZoneId: null,
    RecorderUtcOffsetMinutes: 0,
    LevelSessions: [],
  } satisfies ActivityAppSession;
}

describe("activity connection errors", () => {
  test("maps protocol mismatches to the update-required state", () => {
    const error = new ActivityProtocolMismatchError(1, 2, "0.2.0");

    expect(connectionStatusForError(error)).toBe("incompatible");
  });

  test("keeps ordinary IPC failures in the offline error state", () => {
    expect(connectionStatusForError(new Error("Connection refused"))).toBe("error");
  });
});

describe("recent activity polling", () => {
  test("replaces the refreshed window and preserves older cached sessions", () => {
    const current = [
      appSession("stale-newer", "2026-07-30T05:00:00Z"),
      appSession("session-4", "2026-07-30T04:00:00Z"),
      appSession("session-3", "2026-07-30T03:00:00Z"),
      appSession("session-2", "2026-07-30T02:00:00Z"),
    ];
    const updated = appSession("session-4", "2026-07-30T04:00:00Z", "2026-07-30T04:30:00Z");
    const recent = [appSession("session-6", "2026-07-30T06:00:00Z"), updated];

    const merged = mergeRecentAppSessions(current, recent, 2);

    expect(merged.map((session) => session.Id)).toEqual([
      "session-6",
      "session-4",
      "session-3",
      "session-2",
    ]);
    expect(merged[1]).toBe(updated);
  });

  test("replaces the cache when the bounded response contains the complete history", () => {
    const current = [
      appSession("session-2", "2026-07-30T02:00:00Z"),
      appSession("deleted", "2026-07-30T01:00:00Z"),
    ];
    const recent = [appSession("session-2", "2026-07-30T02:00:00Z")];

    expect(mergeRecentAppSessions(current, recent, 2)).toEqual(recent);
  });
});
