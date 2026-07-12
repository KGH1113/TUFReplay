import type { ActivityAppSession, ActivityDay, ActivityRun, RunMarker } from "../activity.model";

export function dateKeyInTimeZone(utc: string, timeZone: string): string {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date(utc));
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? "";
  return `${get("year")}-${get("month")}-${get("day")}`;
}

export function groupSessionsByDay(sessions: ActivityAppSession[], timeZone: string): ActivityDay[] {
  const groups = new Map<string, ActivityAppSession[]>();
  for (const session of sessions) {
    const key = dateKeyInTimeZone(session.StartedAtUtc, timeZone);
    groups.set(key, [...(groups.get(key) ?? []), session]);
  }
  return [...groups.entries()]
    .sort(([left], [right]) => right.localeCompare(left))
    .map(([date, appSessions]) => {
      const levelSessions = appSessions.flatMap((session) => session.LevelSessions);
      return {
        date,
        appSessions,
        levelSessions,
        runCount: levelSessions.reduce((sum, session) => sum + session.RunCount, 0),
        clearRunCount: levelSessions.reduce((sum, session) => sum + session.ClearRunCount, 0),
      };
    });
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
      bestLastFloorIndex: markerRuns.reduce((best, run) => Math.max(best, run.LastTile ?? run.StartTile), floorIndex),
    }));
}

export function isClearRun(run: ActivityRun): boolean {
  return run.Result.toLowerCase() === "cleared" || run.Result.toLowerCase() === "completed";
}

export function runsForMarker(runs: ActivityRun[], marker: RunMarker | null): ActivityRun[] {
  return marker ? runs.filter((run) => run.StartTile === marker.floorIndex) : [];
}
