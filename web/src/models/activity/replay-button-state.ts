import type { ActivityRun } from "@/models/activity/activity-model";

export function replayButtonState(run: ActivityRun, readOnly: boolean, replayPending: boolean) {
  const permanentlyUnavailable = !run.replayPlayable;
  return {
    permanentlyUnavailable,
    disabled: readOnly || permanentlyUnavailable,
    ariaDisabled: readOnly || permanentlyUnavailable || replayPending,
    tooltipTabIndex: permanentlyUnavailable ? 0 : undefined,
    cursor: permanentlyUnavailable
      ? "not-allowed"
      : readOnly
        ? "default"
        : replayPending
          ? "wait"
          : "pointer",
  } as const;
}
