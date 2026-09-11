import type { ActivityRun, AppSession } from "@/models/activity/activity-model";

export const RECENT_ACTIVITY_SESSION_LIMIT = 20;

export const activityQueryKeys = {
  all: ["activity"] as const,
  sessions: ["activity", "sessions"] as const,
  logicalLevel: (id: string) => ["activity", "logical-level", id] as const,
  logicalLevelRuns: (id: string, appSessionIds: readonly string[]) =>
    ["activity", "logical-level", id, "runs", ...appSessionIds] as const,
  logicalLevelChart: (id: string) => ["activity", "logical-level", id, "chart"] as const,
};

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

export function removeRunById(runs: ActivityRun[], runId: string) {
  return runs.filter((run) => run.id !== runId);
}
