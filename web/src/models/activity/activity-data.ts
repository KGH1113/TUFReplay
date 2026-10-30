import type {
  ActivityDay,
  ActivityRun,
  AppSession,
  LevelCard,
  LevelSession,
  RunMarker,
} from "@/models/activity/activity-model";
import { runOutcome } from "@/models/activity/run-progress";

const dateKeyFormatters = new Map<string, Intl.DateTimeFormat>();

export function groupSessionsByDay(sessions: AppSession[], timeZone: string): ActivityDay[] {
  const latestRevisionByGroup = buildLatestRevisionByGroup(sessions);
  const groups = new Map<string, AppSession[]>();
  for (const session of sessions) {
    const key = dateKeyInTimeZone(session.startedAtUtc, timeZone);
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
        hasOpenableLevels: levelSessions.some((level) => level.canOpen),
        runCount: levelSessions.reduce((sum, level) => sum + level.runCount, 0),
        clearRunCount: levelSessions.reduce((sum, level) => sum + level.clearRunCount, 0),
        lockedRunCount: appSessions.reduce(
          (sum, session) =>
            sum + session.levelSessions.reduce((count, visit) => count + visit.lockedRunCount, 0),
          0,
        ),
      };
    });
}

export function buildLevelCardOverviews(
  sessions: AppSession[],
  latestRevisionByGroup = buildLatestRevisionByGroup(sessions),
) {
  const visitsByGroup = new Map<string, LevelSession[]>();
  for (const visit of sessions.flatMap((session) => session.levelSessions)) {
    const visits = visitsByGroup.get(visit.levelGroupId);
    if (visits) visits.push(visit);
    else visitsByGroup.set(visit.levelGroupId, [visit]);
  }
  const result = new Map<string, LevelCard>();
  const lastOpenedAtByGroup = new Map<string, string>();
  for (const [levelGroupId, visits] of visitsByGroup) {
    const latestVisitForDay = visits.reduce((latest, visit) =>
      isLaterVisit(visit, latest) ? visit : latest,
    );
    lastOpenedAtByGroup.set(levelGroupId, latestVisitForDay.openedAtUtc);
    const latestRevisionId = latestRevisionByGroup.get(levelGroupId);
    const visibleVisits = visits.filter((visit) => visit.logicalLevelId === latestRevisionId);
    const hiddenRunCount = visits
      .filter((visit) => visit.logicalLevelId !== latestRevisionId)
      .reduce((sum, visit) => sum + visit.runCount, 0);
    const canOpen = visibleVisits.length > 0;
    const countedVisits = canOpen ? visibleVisits : visits;
    const primaryVisit = canOpen ? visibleVisits[0] : latestVisitForDay;
    const overview = createLevelCard(levelGroupId, primaryVisit, canOpen, hiddenRunCount);
    for (const visit of countedVisits) if (visit !== primaryVisit) mergeVisit(overview, visit);
    result.set(levelGroupId, overview);
  }
  return new Map(
    [...result].sort(([left], [right]) =>
      (lastOpenedAtByGroup.get(right) ?? "").localeCompare(lastOpenedAtByGroup.get(left) ?? ""),
    ),
  );
}

export function aggregateRunMarkers(runs: ActivityRun[]): RunMarker[] {
  const groups = new Map<number, ActivityRun[]>();
  for (const run of runs) {
    const group = groups.get(run.startTile);
    if (group) group.push(run);
    else groups.set(run.startTile, [run]);
  }
  return [...groups.entries()]
    .sort(([left], [right]) => left - right)
    .map(([floorIndex, markerRuns]) => ({
      id: `floor-${floorIndex}`,
      floorIndex,
      count: markerRuns.length,
      clearCount: markerRuns.filter(isClearRun).length,
      bestLastFloorIndex: markerRuns.reduce(
        (best, run) => Math.max(best, run.lastTile ?? run.startTile),
        floorIndex,
      ),
    }));
}

export function runsForMarker(runs: ActivityRun[], marker: RunMarker | null) {
  return marker ? runs.filter((run) => run.startTile === marker.floorIndex) : [];
}

/** Same rule as the recorder's clear counts; see `runOutcome`. */
export function isClearRun(run: ActivityRun) {
  return runOutcome(run) === "clear";
}

function buildLatestRevisionByGroup(sessions: AppSession[]) {
  const latest = new Map<string, LevelSession>();
  for (const visit of sessions.flatMap((session) => session.levelSessions)) {
    const current = latest.get(visit.levelGroupId);
    if (!current || isLaterVisit(visit, current)) latest.set(visit.levelGroupId, visit);
  }
  return new Map([...latest].map(([groupId, visit]) => [groupId, visit.logicalLevelId]));
}

function createLevelCard(
  levelGroupId: string,
  visit: LevelSession,
  canOpen: boolean,
  hiddenRunCount: number,
): LevelCard {
  return {
    id: visit.logicalLevelId,
    levelGroupId,
    canOpen,
    visibleRunCount: canOpen ? visit.runCount : 0,
    hiddenRunCount,
    tufLevelId: visit.tufLevelId,
    relativeLevelPath: visit.relativeLevelPath,
    song: visit.song,
    author: visit.author,
    artist: visit.artist,
    firstSeenAtUtc: visit.openedAtUtc,
    lastSeenAtUtc: visit.closedAtUtc ?? visit.openedAtUtc,
    floorCount: visit.floorCount,
    visitCount: 1,
    runCount: visit.runCount,
    clearRunCount: visit.clearRunCount,
    noFailRunCount: visit.noFailRunCount,
    firstStartTile: visit.firstStartTile,
    lastStartTile: visit.lastStartTile,
    chartAvailable: visit.chartAvailable,
  };
}

function mergeVisit(current: LevelCard, visit: LevelSession) {
  current.tufLevelId ??= visit.tufLevelId;
  current.song ??= visit.song;
  current.author ??= visit.author;
  current.artist ??= visit.artist;
  if (visit.openedAtUtc < current.firstSeenAtUtc) current.firstSeenAtUtc = visit.openedAtUtc;
  const visitEnd = visit.closedAtUtc ?? visit.openedAtUtc;
  if (visitEnd > current.lastSeenAtUtc) current.lastSeenAtUtc = visitEnd;
  current.floorCount = Math.max(current.floorCount, visit.floorCount);
  current.visitCount += 1;
  current.runCount += visit.runCount;
  if (current.canOpen) current.visibleRunCount += visit.runCount;
  current.clearRunCount += visit.clearRunCount;
  current.noFailRunCount += visit.noFailRunCount;
  current.firstStartTile = minNullable(current.firstStartTile, visit.firstStartTile);
  current.lastStartTile = maxNullable(current.lastStartTile, visit.lastStartTile);
  current.chartAvailable ||= visit.chartAvailable;
}

function isLaterVisit(left: LevelSession, right: LevelSession) {
  return (
    left.openedAtUtc > right.openedAtUtc ||
    (left.openedAtUtc === right.openedAtUtc && left.id > right.id)
  );
}

/** The `YYYY-MM-DD` day an instant falls on in a time zone; days group by this key. */
export function dateKeyInTimeZone(value: string, timeZone: string) {
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
  const parts = formatter.formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";
  return `${part("year")}-${part("month")}-${part("day")}`;
}

function minNullable(left: number | null, right: number | null) {
  if (left === null) return right;
  if (right === null) return left;
  return Math.min(left, right);
}

function maxNullable(left: number | null, right: number | null) {
  if (left === null) return right;
  if (right === null) return left;
  return Math.max(left, right);
}

export interface LevelRunSummary {
  /** Furthest percent reached by an attempt that started at tile zero, or null without one. */
  bestFromStartPercent: number | null;
  clearCount: number;
  lockedCount: number;
}

export function summarizeLevelRuns(runs: ActivityRun[]): LevelRunSummary {
  let bestTile: number | null = null;
  let floorCount = 1;
  let clearCount = 0;
  let lockedCount = 0;
  for (const run of runs) {
    if (isClearRun(run)) clearCount += 1;
    if (run.locked) lockedCount += 1;
    if (run.startTile !== 0) continue;
    const reached = Math.max(run.startTile, run.lastTile ?? run.startTile);
    if (bestTile === null || reached / Math.max(1, run.floorCount) > bestTile / floorCount) {
      bestTile = reached;
      floorCount = Math.max(1, run.floorCount);
    }
  }
  return {
    bestFromStartPercent:
      bestTile === null ? null : Math.round(Math.min(1, bestTile / floorCount) * 100),
    clearCount,
    lockedCount,
  };
}
