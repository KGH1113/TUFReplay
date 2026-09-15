import type { ActivityRun } from "@/models/activity/activity-model";

export function replayButtonState(run: ActivityRun, readOnly: boolean, replayBusy: boolean) {
  const permanentlyUnavailable = !run.replayPlayable;
  return {
    permanentlyUnavailable,
    disabled: readOnly || permanentlyUnavailable || replayBusy,
    ariaDisabled: readOnly || permanentlyUnavailable || replayBusy,
    tooltipTabIndex: permanentlyUnavailable ? 0 : undefined,
    cursor: permanentlyUnavailable
      ? "not-allowed"
      : readOnly
        ? "default"
        : replayBusy
          ? "wait"
          : "pointer",
  } as const;
}
