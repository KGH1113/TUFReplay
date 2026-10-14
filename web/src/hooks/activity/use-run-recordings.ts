import { useEffect, useRef, useState } from "react";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import {
  type RecordingAction,
  type RecordingKind,
  type RunRecordingActions,
  runRecording,
} from "@/models/activity/run-recordings";

export function useRunRecordings(
  run: ActivityRun,
  open: boolean,
  disabled: boolean,
  deletionDisabled: boolean,
  actions: RunRecordingActions,
  failureMessage: (kind: RecordingKind, action: RecordingAction) => string,
) {
  const [kind, setKind] = useState<RecordingKind>(
    run.hasMicrophoneRecording ? "microphone" : "camera",
  );
  const [pending, setPending] = useState<RecordingAction | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [error, setError] = useState("");
  const busy = pending !== null;
  const deleteTrigger = useRef<HTMLButtonElement>(null);
  const deleteCancel = useRef<HTMLButtonElement>(null);
  const activeTab = useRef<HTMLButtonElement>(null);
  const wasConfirming = useRef(false);

  useEffect(() => {
    if (!open) {
      setError("");
      setConfirmDelete(false);
    }
  }, [open]);

  useEffect(() => {
    if (confirmDelete) deleteCancel.current?.focus({ preventScroll: true });
    else if (open && wasConfirming.current)
      (deleteTrigger.current ?? activeTab.current)?.focus({ preventScroll: true });
    wasConfirming.current = confirmDelete;
  }, [confirmDelete, open]);

  const act = async (action: RecordingAction) => {
    const recording = runRecording(run, kind);
    if (
      disabled ||
      !open ||
      busy ||
      !recording.available ||
      (action === "delete" && deletionDisabled) ||
      (action === "keep" && recording.permanent)
    )
      return;
    setPending(action);
    setError("");
    try {
      await actions[kind][action](run);
      setConfirmDelete(false);
    } catch (cause) {
      setError(localizedErrorMessage(cause, failureMessage(kind, action)));
    } finally {
      setPending(null);
    }
  };

  return {
    kind,
    pending,
    busy,
    error,
    confirmDelete,
    setConfirmDelete,
    deleteTrigger,
    deleteCancel,
    activeTab,
    recording: runRecording(run, kind),
    selectKind: (next: string) => {
      if (busy || (next !== "microphone" && next !== "camera")) return;
      wasConfirming.current = false;
      setKind(next);
      setConfirmDelete(false);
      setError("");
    },
    act,
  };
}
