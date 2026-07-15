import { useEffect, useRef, useState } from "react";

import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle } from "@/ui/dialog.component";

import type { ActivityRun, ReplayLevelFilePickerStatus } from "../activity.model";

export function ReplayLevelChoiceDialog({
  run,
  pickerStatus,
  playError,
  playErrorRunId,
  onClose,
  onPlay,
  onChooseAnother,
  onResetPicker,
}: {
  run: ActivityRun | null;
  pickerStatus: ReplayLevelFilePickerStatus | null;
  playError: string;
  playErrorRunId: string | null;
  onClose: () => void;
  onPlay: (runId: string, levelPath?: string) => Promise<boolean>;
  onChooseAnother: (runId: string) => Promise<boolean>;
  onResetPicker: () => void;
}) {
  const autoPlayKeyRef = useRef("");
  const [isStarting, setIsStarting] = useState(false);
  const currentPicker = pickerStatus?.RunId === run?.Id ? pickerStatus : null;
  const isPicking = currentPicker?.State === "picking";

  useEffect(() => {
    if (!run || currentPicker?.State !== "selected" || !currentPicker.LevelPath) return;
    const key = `${currentPicker.OperationId}:${currentPicker.LevelPath}`;
    if (autoPlayKeyRef.current === key) return;
    autoPlayKeyRef.current = key;
    setIsStarting(true);
    void onPlay(run.Id, currentPicker.LevelPath).then((started) => {
      setIsStarting(false);
      if (started) {
        onResetPicker();
        onClose();
      } else {
        autoPlayKeyRef.current = "";
      }
    });
  }, [currentPicker, onClose, onPlay, onResetPicker, run]);

  const close = () => {
    if (isPicking) return;
    autoPlayKeyRef.current = "";
    setIsStarting(false);
    onResetPicker();
    onClose();
  };

  const playRecorded = async () => {
    if (!run || isPicking || isStarting) return;
    setIsStarting(true);
    const started = await onPlay(run.Id);
    setIsStarting(false);
    if (started) close();
  };

  const chooseAnother = async () => {
    if (!run || isPicking || isStarting) return;
    autoPlayKeyRef.current = "";
    setIsStarting(true);
    await onChooseAnother(run.Id);
    setIsStarting(false);
  };

  const currentPlayError = playErrorRunId === run?.Id ? playError : "";
  const pickerMessage = currentPlayError || currentPicker?.Message;
  const pickerFailed = Boolean(currentPlayError) || currentPicker?.State === "error";

  return (
    <Dialog open={Boolean(run)} onOpenChange={(open) => !open && close()}>
      <DialogContent
        aria-describedby="replay-level-choice-description"
        onEscapeKeyDown={(event) => isPicking && event.preventDefault()}
        onPointerDownOutside={(event) => isPicking && event.preventDefault()}
      >
        <div className="space-y-2">
          <DialogTitle>Choose replay level</DialogTitle>
          <p id="replay-level-choice-description" className="text-sm text-muted-foreground">
            You can choose another level file with the same tiles and gameplay events. Visual
            effects may differ.
          </p>
        </div>

        <div className="mt-6 grid gap-2">
          <Button
            type="button"
            disabled={isPicking || isStarting}
            onClick={() => void playRecorded()}
          >
            Open level used for this run
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={isPicking || isStarting}
            onClick={() => void chooseAnother()}
          >
            {isPicking ? "Waiting for file selection…" : "Choose another matching level…"}
          </Button>
          <Button type="button" variant="ghost" disabled={isPicking || isStarting} onClick={close}>
            Cancel
          </Button>
        </div>

        {pickerMessage ? (
          <p
            aria-live="polite"
            className={
              pickerFailed ? "mt-4 text-sm text-destructive" : "mt-4 text-sm text-muted-foreground"
            }
          >
            {pickerMessage}
          </p>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
