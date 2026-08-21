import type {
  ActivityAppSession,
  ActivityDay,
  ActivityLevelCardOverview,
  ActivityLevelSessionOverview,
  ActivityRun,
  RunMarker,
} from "../activity.model";

const dateKeyFormatters = new Map<string, Intl.DateTimeFormat>();

export function dateKeyInTimeZone(utc: string, timeZone: string): string {
  let formatter = dateKeyFormatters.get(timeZone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat("en-CA", {
      timeZone,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    });
    dateKeyFormatters.set(timeZone, formatter);
  }
  const parts = formatter.formatToParts(new Date(utc));
  const get = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? "";
  return `${get("year")}-${get("month")}-${get("day")}`;
}

export function groupSessionsByDay(
  sessions: ActivityAppSession[],
  timeZone: string,
): ActivityDay[] {
  const latestRevisionByGroup = buildLatestRevisionByGroup(sessions);
  const groups = new Map<string, ActivityAppSession[]>();
  for (const session of sessions) {
    const key = dateKeyInTimeZone(session.StartedAtUtc, timeZone);
    const group = groups.get(key);
    if (group) group.push(session);
    else groups.set(key, [session]);
  }
  return [...groups.entries()]
    .sort(([left], [right]) => right.localeCompare(left))
    .map(([date, appSessions]) => {
      const levelSessions = [
        ...buildLevelCardOverviews(appSessions, latestRevisionByGroup).values(),
      ];
      return {
        date,
        appSessions,
        levelSessions,
        hasOpenableLevels: levelSessions.some((level) => level.CanOpen),
        runCount: levelSessions.reduce((sum, level) => sum + level.RunCount, 0),
        clearRunCount: levelSessions.reduce((sum, level) => sum + level.ClearRunCount, 0),
      };
    });
}

export function buildLevelCardOverviews(
  sessions: ActivityAppSession[],
  latestRevisionByGroup = buildLatestRevisionByGroup(sessions),
): Map<string, ActivityLevelCardOverview> {
  const visitsByGroup = new Map<string, ActivityLevelSessionOverview[]>();
  for (const visit of sessions.flatMap((session) => session.LevelSessions)) {
    const visits = visitsByGroup.get(visit.LevelGroupId);
    if (visits) visits.push(visit);
    else visitsByGroup.set(visit.LevelGroupId, [visit]);
  }

  const result = new Map<string, ActivityLevelCardOverview>();
  for (const [levelGroupId, visits] of visitsByGroup) {
    const latestVisitForDay = visits.reduce((latest, visit) =>
      isLaterVisit(visit, latest) ? visit : latest,
    );
    const latestRevisionId = latestRevisionByGroup.get(levelGroupId);
    const visibleVisits = visits.filter((visit) => visit.LogicalLevelId === latestRevisionId);
    const hiddenRunCount = visits
      .filter((visit) => visit.LogicalLevelId !== latestRevisionId)
      .reduce((sum, visit) => sum + visit.RunCount, 0);
    const canOpen = visibleVisits.length > 0;
    const countedVisits = canOpen ? visibleVisits : visits;
    const primaryVisit = canOpen ? visibleVisits[0] : latestVisitForDay;
    const overview = createLevelCardOverview(levelGroupId, primaryVisit, canOpen, hiddenRunCount);
    for (const visit of countedVisits)
      if (visit !== primaryVisit) mergeCountedVisit(overview, visit);
    result.set(levelGroupId, overview);
  }
  return result;
}

function buildLatestRevisionByGroup(sessions: ActivityAppSession[]) {
  const latestVisitByGroup = new Map<string, ActivityLevelSessionOverview>();
  for (const visit of sessions.flatMap((session) => session.LevelSessions)) {
    const latest = latestVisitByGroup.get(visit.LevelGroupId);
    if (!latest || isLaterVisit(visit, latest)) latestVisitByGroup.set(visit.LevelGroupId, visit);
  }
  return new Map(
    [...latestVisitByGroup].map(([levelGroupId, visit]) => [levelGroupId, visit.LogicalLevelId]),
  );
}

function createLevelCardOverview(
  levelGroupId: string,
  visit: ActivityLevelSessionOverview,
  canOpen: boolean,
  hiddenRunCount: number,
): ActivityLevelCardOverview {
  return {
    Id: visit.LogicalLevelId,
    LevelGroupId: levelGroupId,
    CanOpen: canOpen,
    VisibleRunCount: canOpen ? visit.RunCount : 0,
    HiddenRunCount: hiddenRunCount,
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
  };
}

function mergeCountedVisit(
  current: ActivityLevelCardOverview,
  visit: ActivityLevelSessionOverview,
) {
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
  if (current.CanOpen) current.VisibleRunCount += visit.RunCount;
  current.ClearRunCount += visit.ClearRunCount;
  current.NoFailRunCount += visit.NoFailRunCount;
  current.FirstStartTile = minNullable(current.FirstStartTile, visit.FirstStartTile);
  current.LastStartTile = maxNullable(current.LastStartTile, visit.LastStartTile);
  current.ChartAvailable ||= visit.ChartAvailable;
}

function isLaterVisit(
  candidate: ActivityLevelSessionOverview,
  current: ActivityLevelSessionOverview,
) {
  const timestampComparison = candidate.OpenedAtUtc.localeCompare(current.OpenedAtUtc);
  return timestampComparison > 0 || (timestampComparison === 0 && candidate.Id > current.Id);
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
  for (const run of runs) {
    const group = groups.get(run.StartTile);
    if (group) group.push(run);
    else groups.set(run.StartTile, [run]);
  }
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
  const result = run.Result.toLowerCase();
  return (result === "cleared" || result === "completed") && run.StartTile === 0 && !run.NoFailMode;
}

export function runsForMarker(runs: ActivityRun[], marker: RunMarker | null): ActivityRun[] {
  return marker ? runs.filter((run) => run.StartTile === marker.floorIndex) : [];
}
