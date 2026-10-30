import type { ActivityRun, JudgmentDifficulty } from "@/models/activity/activity-model";
import { runOutcome } from "@/models/activity/run-progress";

export interface AttemptFilters {
  clearsOnly: boolean;
  lockedOnly: boolean;
  withRecordings: boolean;
  hideNoFail: boolean;
  /** Pitch percentages to keep; empty keeps every pitch. */
  pitches: number[];
  /** Judgment difficulties to keep; empty keeps every difficulty. */
  difficulties: JudgmentDifficulty[];
}

export const noAttemptFilters: AttemptFilters = {
  clearsOnly: false,
  lockedOnly: false,
  withRecordings: false,
  hideNoFail: false,
  pitches: [],
  difficulties: [],
};

const difficultyOrder: JudgmentDifficulty[] = ["Lenient", "Normal", "Strict"];

/** Choices that actually distinguish this level's attempts; a single value is not worth filtering. */
export function attemptFilterOptions(runs: ActivityRun[]) {
  const pitches = new Set<number>();
  const difficulties = new Set<JudgmentDifficulty>();
  for (const run of runs) {
    if (run.levelPitchPercent !== null && run.levelPitchPercent !== undefined)
      pitches.add(run.levelPitchPercent);
    if (run.judgmentDifficulty) difficulties.add(run.judgmentDifficulty);
  }
  return {
    pitches: pitches.size > 1 ? [...pitches].sort((left, right) => left - right) : [],
    difficulties:
      difficulties.size > 1
        ? difficultyOrder.filter((difficulty) => difficulties.has(difficulty))
        : [],
  };
}

export function activeAttemptFilterCount(filters: AttemptFilters) {
  return (
    Number(filters.clearsOnly) +
    Number(filters.lockedOnly) +
    Number(filters.withRecordings) +
    Number(filters.hideNoFail) +
    Number(filters.pitches.length > 0) +
    Number(filters.difficulties.length > 0)
  );
}

export function applyAttemptFilters(runs: ActivityRun[], filters: AttemptFilters) {
  if (activeAttemptFilterCount(filters) === 0) return runs;
  return runs.filter(
    (run) =>
      (!filters.clearsOnly || runOutcome(run) === "clear") &&
      (!filters.lockedOnly || run.locked) &&
      (!filters.withRecordings || run.hasMicrophoneRecording || run.hasCameraRecording) &&
      (!filters.hideNoFail || !run.noFailMode) &&
      (filters.pitches.length === 0 ||
        (run.levelPitchPercent !== null &&
          run.levelPitchPercent !== undefined &&
          filters.pitches.includes(run.levelPitchPercent))) &&
      (filters.difficulties.length === 0 ||
        (run.judgmentDifficulty !== null &&
          run.judgmentDifficulty !== undefined &&
          filters.difficulties.includes(run.judgmentDifficulty))),
  );
}
