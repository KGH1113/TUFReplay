import { useEffect, useRef, useState } from "react";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import {
  type RecordingAction,
  type RecordingKind,
  type RunRecordingAction,
  runRecording,
} from "@/models/activity/run-recordings";

/** Export, keep and delete for one of a run's recordings, shown inline in the run inspector. */
export function useRunRecordingControl({
  run,
  kind,
  disabled,
  deletionDisabled,
  actions,
  failureMessage,
}: {
  run: ActivityRun;
  kind: RecordingKind;
  disabled: boolean;
  deletionDisabled: boolean;
  actions: Record<RecordingAction, RunRecordingAction>;
  failureMessage: (action: RecordingAction) => string;
}) {
  const [pending, setPending] = useState<RecordingAction | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [error, setError] = useState("");
  const deleteTrigger = useRef<HTMLButtonElement>(null);
  const deleteCancel = useRef<HTMLButtonElement>(null);
  const wasConfirming = useRef(false);
  const runId = run.id;
  const recording = runRecording(run, kind);
  const busy = pending !== null;

  useEffect(() => {
    void runId;
    setConfirmDelete(false);
    setError("");
  }, [runId]);

  // Destructive confirmation focuses the safe choice; cancelling returns to the trigger.
  useEffect(() => {
    if (confirmDelete) deleteCancel.current?.focus({ preventScroll: true });
    else if (wasConfirming.current) deleteTrigger.current?.focus({ preventScroll: true });
    wasConfirming.current = confirmDelete;
  }, [confirmDelete]);

  const act = async (action: RecordingAction) => {
    if (
      disabled ||
      busy ||
      !recording.available ||
      (action === "delete" && deletionDisabled) ||
      (action === "keep" && recording.permanent)
    )
      return;
    setPending(action);
    setError("");
    try {
      await actions[action](run);
      setConfirmDelete(false);
    } catch (cause) {
      setError(localizedErrorMessage(cause, failureMessage(action)));
    } finally {
      setPending(null);
    }
  };

  return {
    recording,
    pending,
    busy,
    error,
    confirmDelete,
    requestDelete: () => {
      if (!busy && !deletionDisabled && !disabled) setConfirmDelete(true);
    },
    cancelDelete: () => setConfirmDelete(false),
    deleteTrigger,
    deleteCancel,
    act,
  };
}

export type { RecordingKind };
