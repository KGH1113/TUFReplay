import type { ActivityRun } from "@/models/activity/activity-model";

export type RunOutcome = "clear" | "finished" | "failed" | "quit" | "unknown";

type ProgressRun = Pick<ActivityRun, "startTile" | "lastTile" | "floorCount">;

/** Percent of the level reached at a tile, rounded the same way everywhere in the UI. */
export function tileProgressPercent(tile: number, floorCount: number) {
  return Math.round(Math.min(1, Math.max(0, tile) / Math.max(1, floorCount)) * 100);
}

export function runEndTile(run: ProgressRun) {
  return Math.max(run.startTile, run.lastTile ?? run.startTile);
}

export function runStartPercent(run: ProgressRun) {
  return tileProgressPercent(run.startTile, run.floorCount);
}

export function runEndPercent(run: ProgressRun) {
  return tileProgressPercent(runEndTile(run), run.floorCount);
}

/**
 * A clear is a full run from tile zero. A No-Fail run counts too when nothing in it would have
 * ended a normal run (no Miss, no Overload). Reaching the end any other way is "finished", so
 * players can tell practice from a submittable clear.
 */
export function runOutcome(
  run: Pick<ActivityRun, "result" | "startTile" | "noFailMode" | "judgmentCounts">,
): RunOutcome {
  const result = run.result.toLowerCase();
  if (result === "cleared" || result === "completed")
    return run.startTile === 0 && survivedWithoutNoFail(run) ? "clear" : "finished";
  if (result === "failed") return "failed";
  if (result === "aborted") return "quit";
  return "unknown";
}

/** Misses and overloads end a normal run; without counts a No-Fail run can't be trusted. */
function survivedWithoutNoFail(run: Pick<ActivityRun, "noFailMode" | "judgmentCounts">) {
  if (!run.noFailMode) return true;
  const counts = run.judgmentCounts;
  return Boolean(counts) && (counts?.Miss ?? 0) === 0 && (counts?.Overload ?? 0) === 0;
}

export function runDurationMs(run: Pick<ActivityRun, "startedAtUtc" | "endedAtUtc">) {
  if (!run.endedAtUtc) return null;
  const started = Date.parse(run.startedAtUtc);
  const ended = Date.parse(run.endedAtUtc);
  if (!Number.isFinite(started) || !Number.isFinite(ended) || ended < started) return null;
  return ended - started;
}

/** `m:ss` for short spans, `h:mm:ss` past an hour. */
export function formatDuration(durationMs: number | null) {
  if (durationMs === null) return null;
  const totalSeconds = Math.round(durationMs / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = String(totalSeconds % 60).padStart(2, "0");
  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, "0")}:${seconds}`
    : `${minutes}:${seconds}`;
}
