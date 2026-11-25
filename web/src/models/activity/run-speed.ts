import type { ActivityRun } from "@/models/activity/activity-model";

/**
 * The speed a run was played at, in percent: the level's pitch times the speed set in game,
 * like the editor's Ctrl+Space speed. A 100% level played with Ctrl+Space at 75% is 75.
 * Runs without the played speed show the level's pitch.
 */
export function runSpeedPercent(
  run: Pick<ActivityRun, "effectivePitch" | "levelPitchPercent">,
): number | null {
  const pitch = run.effectivePitch;
  if (typeof pitch === "number" && Number.isFinite(pitch) && pitch > 0)
    return Math.round(pitch * 10_000) / 100;
  return run.levelPitchPercent ?? null;
}
