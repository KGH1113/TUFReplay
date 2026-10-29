import type { ActivityRun, AppSession } from "@/models/activity/activity-model";

export const RECENT_ACTIVITY_SESSION_LIMIT = 20;

export const activityQueryKeys = {
  all: ["activity"] as const,
  sessions: ["activity", "sessions"] as const,
  logicalLevel: (id: string) => ["activity", "logical-level", id] as const,
  logicalLevelRuns: (id: string, date: string | null) =>
    ["activity", "logical-level", id, "runs", date] as const,
  logicalLevelChart: (id: string) => ["activity", "logical-level", id, "chart"] as const,
};

/**
 * A logical level's chart text belongs to one immutable gameplay revision, so a new run
 * never changes it. Producing it makes the game re-read and validate the whole level
 * file, which creates enough garbage to delay ADOFAI's async input during the retry.
 */
export function refreshesOnActivityChange(queryKey: readonly unknown[]) {
  return !(
    queryKey[0] === "activity" &&
    queryKey[1] === "logical-level" &&
    queryKey[3] === "chart"
  );
}

export function mergeRecentAppSessions(
  current: AppSession[],
  recent: AppSession[],
  limit = RECENT_ACTIVITY_SESSION_LIMIT,
) {
  if (recent.length < limit) return recent;
  const recentIds = new Set(recent.map((session) => session.id));
  const boundary = recent[recent.length - 1];
  const older = current.filter(
    (session) =>
      !recentIds.has(session.id) &&
      (session.startedAtUtc < boundary.startedAtUtc ||
        (session.startedAtUtc === boundary.startedAtUtc && session.id < boundary.id)),
  );
  return [...recent, ...older];
}

/**
 * Applies a lock change to the cached level session. The activity refresh only rereads the
 * most recent sessions, so older days would otherwise keep a stale lock count.
 */
export function adjustLockedRunCounts(
  sessions: AppSession[],
  levelSessionId: string,
  lockedDelta: number,
  lockedClearDelta: number,
) {
  return sessions.map((session) =>
    session.levelSessions.some((visit) => visit.id === levelSessionId)
      ? {
          ...session,
          levelSessions: session.levelSessions.map((visit) =>
            visit.id === levelSessionId
              ? {
                  ...visit,
                  lockedRunCount: Math.max(0, visit.lockedRunCount + lockedDelta),
                  lockedClearRunCount: Math.max(0, visit.lockedClearRunCount + lockedClearDelta),
                }
              : visit,
          ),
        }
      : session,
  );
}

export function removeRunById(runs: ActivityRun[], runId: string) {
  return runs.filter((run) => run.id !== runId);
}
