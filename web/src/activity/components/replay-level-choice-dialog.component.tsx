import {
  ArrowRight02Icon,
  Loading03Icon,
  MusicNote01Icon,
  PlayIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useRef, useState } from "react";

import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle } from "@/ui/dialog.component";

import type { ActivityRun, ReplayLevelFilePickerResult } from "../activity.model";

export function ReplayLevelChoiceDialog({
  run,
  pickerResult,
  pickingRunId,
  playError,
  playErrorRunId,
  onClose,
  onPlay,
  onChooseAnother,
  onResetPicker,
}: {
  run: ActivityRun | null;
  pickerResult: ReplayLevelFilePickerResult | null;
  pickingRunId: string | null;
  playError: string;
  playErrorRunId: string | null;
  onClose: () => void;
  onPlay: (runId: string, levelPath?: string) => Promise<boolean>;
  onChooseAnother: (runId: string) => Promise<boolean>;
  onResetPicker: () => void;
}) {
  const autoPlayKeyRef = useRef("");
  const [startingAction, setStartingAction] = useState<"original" | "picker" | null>(null);
  const currentPicker = pickerResult?.RunId === run?.Id ? pickerResult : null;
  const isPicking = pickingRunId === run?.Id;

  useEffect(() => {
    if (!run || currentPicker?.Outcome !== "selected" || !currentPicker.LevelPath) return;
    const key = `${run.Id}:${currentPicker.LevelPath}`;
    if (autoPlayKeyRef.current === key) return;
    autoPlayKeyRef.current = key;
    setStartingAction("picker");
    void onPlay(run.Id, currentPicker.LevelPath).then((started) => {
      setStartingAction(null);
      if (started) {
        onResetPicker();
        onClose();
      }
    });
  }, [currentPicker, onClose, onPlay, onResetPicker, run]);

  const close = () => {
    if (isPicking) return;
    autoPlayKeyRef.current = "";
    setStartingAction(null);
    onResetPicker();
    onClose();
  };

  const playRecorded = async () => {
    if (!run || isPicking || startingAction) return;
    setStartingAction("original");
    const started = await onPlay(run.Id);
    setStartingAction(null);
    if (started) close();
  };

  const chooseAnother = async () => {
    if (!run || isPicking || startingAction) return;
    autoPlayKeyRef.current = "";
    await onChooseAnother(run.Id);
  };

  const currentPlayError = playErrorRunId === run?.Id ? playError : "";
  const pickerMessage =
    currentPlayError ||
    (currentPicker?.Outcome === "mismatch" || currentPicker?.Outcome === "error"
      ? currentPicker.Message
      : null);
  const pickerFailed =
    Boolean(currentPlayError) ||
    currentPicker?.Outcome === "mismatch" ||
    currentPicker?.Outcome === "error";
  const busy = isPicking || startingAction !== null;
  const originalBusy = startingAction === "original";
  const pickerBusy = isPicking || startingAction === "picker";

  return (
    <Dialog open={Boolean(run)} onOpenChange={(open) => !open && close()}>
      <DialogContent
        aria-describedby="replay-level-choice-description"
        className="min-h-0 w-[min(27rem,calc(100vw-2rem))] overflow-hidden p-0 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
        onEscapeKeyDown={(event) => isPicking && event.preventDefault()}
        onPointerDownOutside={(event) => isPicking && event.preventDefault()}
      >
        <div className="px-5 pb-4 pt-5">
          <DialogTitle>Play replay</DialogTitle>
          <p
            id="replay-level-choice-description"
            className="mt-1.5 text-sm leading-relaxed text-muted-foreground"
          >
            Use the original level or choose another file with matching gameplay.
          </p>
        </div>

        <div className="border-y border-border p-2">
          <button
            type="button"
            disabled={busy}
            onClick={() => void playRecorded()}
            className="group flex w-full items-center gap-3 rounded-lg bg-primary/[0.06] p-3 text-left transition-colors hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring disabled:cursor-wait disabled:opacity-60"
          >
            <span className="grid size-9 shrink-0 place-items-center rounded-md bg-primary/10 text-primary">
              <HugeiconsIcon
                aria-hidden="true"
                icon={PlayIcon}
                className="size-4"
                strokeWidth={2}
              />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block text-sm font-medium">Original level</span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                {originalBusy ? "Starting replay…" : "Use the level recorded for this run"}
              </span>
            </span>
            {originalBusy ? (
              <HugeiconsIcon
                aria-hidden="true"
                icon={Loading03Icon}
                className="size-4 animate-spin text-primary"
                strokeWidth={2}
              />
            ) : (
              <span className="text-[10px] font-semibold uppercase tracking-wide text-primary">
                Default
              </span>
            )}
          </button>

          <button
            type="button"
            disabled={busy}
            onClick={() => void chooseAnother()}
            className="group mt-1 flex w-full items-center gap-3 rounded-lg p-3 text-left transition-colors hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring disabled:cursor-wait disabled:opacity-60"
          >
            <span className="grid size-9 shrink-0 place-items-center rounded-md bg-muted text-muted-foreground transition-colors group-hover:text-foreground">
              <HugeiconsIcon
                aria-hidden="true"
                icon={MusicNote01Icon}
                className="size-4"
                strokeWidth={2}
              />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block text-sm font-medium">Choose matching level…</span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                {isPicking
                  ? "Choose a file, then verifying gameplay…"
                  : startingAction === "picker"
                    ? currentPicker?.Outcome === "selected"
                      ? "Starting replay…"
                      : "Opening file picker…"
                    : "Use another file with identical gameplay"}
              </span>
            </span>
            {pickerBusy ? (
              <HugeiconsIcon
                aria-hidden="true"
                icon={Loading03Icon}
                className="size-4 animate-spin text-muted-foreground"
                strokeWidth={2}
              />
            ) : (
              <HugeiconsIcon
                aria-hidden="true"
                icon={ArrowRight02Icon}
                className="size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5"
                strokeWidth={2}
              />
            )}
          </button>
        </div>

        {pickerMessage ? (
          <p
            aria-live="polite"
            className={
              pickerFailed
                ? "px-5 pt-4 text-sm text-destructive"
                : "px-5 pt-4 text-sm text-muted-foreground"
            }
          >
            {pickerMessage}
          </p>
        ) : null}

        <div className="flex justify-end px-4 py-3">
          <Button type="button" variant="ghost" size="sm" disabled={busy} onClick={close}>
            Cancel
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
