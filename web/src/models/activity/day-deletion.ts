import type { ActivityDay } from "@/models/activity/activity-model";

export interface DayDeletionSummary {
  /** Every run on the day, including older level versions the level strip hides. */
  totalRunCount: number;
  lockedRunCount: number;
  unlockedClearRunCount: number;
  deleteRunCount: number;
  keepRunCount: number;
  /** Runs from older level versions; the day list does not count them. */
  hiddenRunCount: number;
}

/** Mirrors the recorder: a day delete removes every unlocked run of the day's app sessions. */
export function summarizeDayDeletion(day: ActivityDay, keepClears: boolean): DayDeletionSummary {
  let totalRunCount = 0;
  let lockedRunCount = 0;
  let unlockedClearRunCount = 0;
  for (const session of day.appSessions)
    for (const visit of session.levelSessions) {
      totalRunCount += visit.runCount;
      lockedRunCount += visit.lockedRunCount;
      unlockedClearRunCount += Math.max(0, visit.clearRunCount - visit.lockedClearRunCount);
    }
  const keepRunCount = lockedRunCount + (keepClears ? unlockedClearRunCount : 0);
  return {
    totalRunCount,
    lockedRunCount,
    unlockedClearRunCount,
    deleteRunCount: Math.max(0, totalRunCount - keepRunCount),
    keepRunCount,
    hiddenRunCount: Math.max(0, totalRunCount - day.runCount),
  };
}

/** Why a day can't be deleted, or null when it can. */
export function dayDeleteBlocker(day: ActivityDay): "empty" | "all_locked" | null {
  const summary = summarizeDayDeletion(day, false);
  if (summary.totalRunCount === 0) return "empty";
  return summary.deleteRunCount === 0 ? "all_locked" : null;
}
