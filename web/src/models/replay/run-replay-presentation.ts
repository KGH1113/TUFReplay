import type { TFunction } from "i18next";
import type { ActivityRun } from "@/models/activity/activity-model";
import { translatedDomainError } from "@/models/activity/localized-error";
import { isReplayInProgress, type ReplayStatus } from "@/models/replay/replay-model";

/** What a run's play control should look like right now. */
export type RunReplayPhase = "ready" | "unavailable" | "starting" | "playing" | "failed";

export interface RunReplayContext {
  status: ReplayStatus;
  pendingRunId: string | null;
  error: string;
  errorRunId: string | null;
  readOnly: boolean;
}

const startingStates = new Set<ReplayStatus["state"]>([
  "preparing",
  "opening_level",
  "waiting_for_focus",
  "starting",
  "returning_to_editor",
]);

export function runReplayPhase(run: ActivityRun, context: RunReplayContext): RunReplayPhase {
  const { status, pendingRunId, error, errorRunId } = context;
  if (!run.replayPlayable) return "unavailable";
  const current = status.runId === run.id;
  if (pendingRunId === run.id || (current && startingStates.has(status.state))) return "starting";
  if (current && status.state === "playing") return "playing";
  if ((errorRunId === run.id && error) || (current && status.state === "error")) return "failed";
  return "ready";
}

/** True while this run is part of an active replay, which also blocks deleting it. */
export function runReplayActive(run: ActivityRun, context: RunReplayContext) {
  return (
    context.pendingRunId === run.id ||
    (context.status.runId === run.id && isReplayInProgress(context.status))
  );
}

/** Another run's replay (or a pending request) blocks starting this one. */
export function replayBusy(context: RunReplayContext) {
  return context.pendingRunId !== null || isReplayInProgress(context.status);
}

/** The single sentence describing this run's replay state, or null when there is nothing to say. */
export function describeRunReplay(
  run: ActivityRun,
  context: RunReplayContext,
  activityT: TFunction<"activity">,
  replayT: TFunction<"replay">,
): string | null {
  if (run.replayUnavailableReason)
    return activityT(`run.replayUnavailable.${run.replayUnavailableReason}`);
  if (!run.replayPlayable) return activityT("run.replayUnavailableGeneric");
  const { status, pendingRunId, error, errorRunId } = context;
  if (pendingRunId === run.id) return replayT("status.sending");
  if (errorRunId === run.id && error) return error;
  if (status.runId === run.id) {
    if (status.state === "preparing") return replayT("status.preparing");
    if (status.state === "opening_level") return replayT("status.openingLevel");
    if (status.state === "waiting_for_focus") return replayT("status.waitingForFocus");
    if (status.state === "starting") return replayT("status.starting");
    if (status.state === "playing") return replayT("status.playing");
    if (status.state === "returning_to_editor") return replayT("status.returning");
    if (status.state === "completed") return replayT("status.completed");
    if (status.state === "cancelled") return replayT("status.cancelled");
    if (status.state === "error")
      return (
        translatedDomainError(status.errorCode ?? "") ||
        status.message ||
        status.errorCode ||
        replayT("status.failed")
      );
    return null;
  }
  if (isReplayInProgress(status)) return replayT("status.replaceActive");
  return null;
}
