import { useEffect, useRef, useState } from "react";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import {
  availableRecordingKind,
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
  const closeButton = useRef<HTMLButtonElement>(null);
  const keepStatus = useRef<HTMLSpanElement>(null);
  const completedKeep = useRef(false);
  const wasConfirming = useRef(false);
  const selectedKind = availableRecordingKind(run, kind) ?? kind;
  const preferredAvailable = runRecording(run, kind).available;
  const recording = runRecording(run, selectedKind);

  useEffect(() => {
    if (!open) {
      setError("");
      setConfirmDelete(false);
      completedKeep.current = false;
    }
  }, [open]);

  useEffect(() => {
    if (confirmDelete) deleteCancel.current?.focus({ preventScroll: true });
    else if (open && wasConfirming.current)
      (deleteTrigger.current ?? activeTab.current)?.focus({ preventScroll: true });
    wasConfirming.current = confirmDelete;
  }, [confirmDelete, open]);

  useEffect(() => {
    if (open && !busy && !preferredAvailable) {
      setKind(selectedKind);
      setConfirmDelete(false);
      (activeTab.current?.disabled ? closeButton.current : activeTab.current)?.focus({
        preventScroll: true,
      });
    }
  }, [open, busy, preferredAvailable, selectedKind]);

  useEffect(() => {
    if (open && !busy && recording.permanent && completedKeep.current) {
      completedKeep.current = false;
      keepStatus.current?.focus({ preventScroll: true });
    }
  }, [open, busy, recording.permanent]);

  const act = async (action: RecordingAction) => {
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
      await actions[selectedKind][action](run);
      if (action === "keep") completedKeep.current = true;
      setConfirmDelete(false);
    } catch (cause) {
      setError(localizedErrorMessage(cause, failureMessage(selectedKind, action)));
    } finally {
      setPending(null);
    }
  };

  return {
    kind: selectedKind,
    pending,
    busy,
    error,
    confirmDelete,
    setConfirmDelete,
    deleteTrigger,
    deleteCancel,
    activeTab,
    closeButton,
    keepStatus,
    recording,
    selectKind: (next: string) => {
      if (
        busy ||
        (next !== "microphone" && next !== "camera") ||
        !runRecording(run, next).available
      )
        return;
      wasConfirming.current = false;
      completedKeep.current = false;
      setKind(next);
      setConfirmDelete(false);
      setError("");
    },
    act,
  };
}
