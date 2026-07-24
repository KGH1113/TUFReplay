import type {
  ActivityAppSession,
  ActivityDay,
  ActivityLogicalLevelOverview,
  ActivityRun,
  RunMarker,
} from "../activity.model";

export function dateKeyInTimeZone(utc: string, timeZone: string): string {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date(utc));
  const get = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? "";
  return `${get("year")}-${get("month")}-${get("day")}`;
}

export function groupSessionsByDay(
  sessions: ActivityAppSession[],
  timeZone: string,
): ActivityDay[] {
  const logicalLevels = buildLogicalLevelOverviews(sessions);
  const groups = new Map<string, ActivityAppSession[]>();
  for (const session of sessions) {
    const key = dateKeyInTimeZone(session.StartedAtUtc, timeZone);
    groups.set(key, [...(groups.get(key) ?? []), session]);
  }
  return [...groups.entries()]
    .sort(([left], [right]) => right.localeCompare(left))
    .map(([date, appSessions]) => {
      const visits = appSessions.flatMap((session) => session.LevelSessions);
      const logicalIds = new Set(visits.map((visit) => visit.LogicalLevelId));
      const levelSessions = [...logicalIds]
        .map((id) => logicalLevels.get(id))
        .filter((level): level is ActivityLogicalLevelOverview => level !== undefined);
      return {
        date,
        appSessions,
        levelSessions,
        runCount: visits.reduce((sum, session) => sum + session.RunCount, 0),
        clearRunCount: visits.reduce((sum, session) => sum + session.ClearRunCount, 0),
      };
    });
}

export function buildLogicalLevelOverviews(
  sessions: ActivityAppSession[],
): Map<string, ActivityLogicalLevelOverview> {
  const result = new Map<string, ActivityLogicalLevelOverview>();
  for (const visit of sessions.flatMap((session) => session.LevelSessions)) {
    const current = result.get(visit.LogicalLevelId);
    if (!current) {
      result.set(visit.LogicalLevelId, {
        Id: visit.LogicalLevelId,
        TufLevelId: visit.TufLevelId,
        Song: visit.Song,
        Author: visit.Author,
        Artist: visit.Artist,
        FirstSeenAtUtc: visit.OpenedAtUtc,
        LastSeenAtUtc: visit.ClosedAtUtc ?? visit.OpenedAtUtc,
        FloorCount: visit.FloorCount,
        VisitCount: 1,
        RunCount: visit.RunCount,
        ClearRunCount: visit.ClearRunCount,
        NoFailRunCount: visit.NoFailRunCount,
        FirstStartTile: visit.FirstStartTile,
        LastStartTile: visit.LastStartTile,
        ChartAvailable: visit.ChartAvailable,
      });
      continue;
    }
    current.TufLevelId ??= visit.TufLevelId;
    current.Song ??= visit.Song;
    current.Author ??= visit.Author;
    current.Artist ??= visit.Artist;
    if (visit.OpenedAtUtc < current.FirstSeenAtUtc) current.FirstSeenAtUtc = visit.OpenedAtUtc;
    const visitEnd = visit.ClosedAtUtc ?? visit.OpenedAtUtc;
    if (visitEnd > current.LastSeenAtUtc) current.LastSeenAtUtc = visitEnd;
    current.FloorCount = Math.max(current.FloorCount, visit.FloorCount);
    current.VisitCount += 1;
    current.RunCount += visit.RunCount;
    current.ClearRunCount += visit.ClearRunCount;
    current.NoFailRunCount += visit.NoFailRunCount;
    current.FirstStartTile = minNullable(current.FirstStartTile, visit.FirstStartTile);
    current.LastStartTile = maxNullable(current.LastStartTile, visit.LastStartTile);
    current.ChartAvailable ||= visit.ChartAvailable;
  }
  return result;
}

function minNullable(left: number | null, right: number | null): number | null {
  if (left === null) return right;
  if (right === null) return left;
  return Math.min(left, right);
}

function maxNullable(left: number | null, right: number | null): number | null {
  if (left === null) return right;
  if (right === null) return left;
  return Math.max(left, right);
}

export function aggregateRunMarkers(runs: ActivityRun[]): RunMarker[] {
  const groups = new Map<number, ActivityRun[]>();
  for (const run of runs) groups.set(run.StartTile, [...(groups.get(run.StartTile) ?? []), run]);
  return [...groups.entries()]
    .sort(([left], [right]) => left - right)
    .map(([floorIndex, markerRuns]) => ({
      id: `floor-${floorIndex}`,
      floorIndex,
      count: markerRuns.length,
      clearCount: markerRuns.filter(isClearRun).length,
      bestLastFloorIndex: markerRuns.reduce(
        (best, run) => Math.max(best, run.LastTile ?? run.StartTile),
        floorIndex,
      ),
    }));
}

export function isClearRun(run: ActivityRun): boolean {
  return run.Result.toLowerCase() === "cleared" || run.Result.toLowerCase() === "completed";
}

export function runsForMarker(runs: ActivityRun[], marker: RunMarker | null): ActivityRun[] {
  return marker ? runs.filter((run) => run.StartTile === marker.floorIndex) : [];
}
