import {
  ArrowRight02Icon,
  Loading03Icon,
  MusicNote01Icon,
  PlayIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ActivityRun } from "@/models/activity/activity-model";
import { translatedDomainError } from "@/models/activity/localized-error";
import type { ReplayLevelFilePickerResult, ReplayStatus } from "@/models/replay/replay-model";
import { Button } from "@/shared/ui/button";
import { Dialog, DialogContent, DialogTitle } from "@/shared/ui/dialog";

export function ReplayLevelChoiceDialog({
  run,
  pickerResult,
  pickingRunId,
  replayStatus,
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
  replayStatus: ReplayStatus;
  playError: string;
  playErrorRunId: string | null;
  onClose: () => void;
  onPlay: (runId: string, levelPath?: string) => Promise<boolean>;
  onChooseAnother: (runId: string) => Promise<boolean>;
  onResetPicker: () => void;
}) {
  const { t } = useTranslation("replay");
  const { t: commonT } = useTranslation("common");
  const autoPlayKeyRef = useRef("");
  const [startingAction, setStartingAction] = useState<"original" | "picker" | null>(null);
  const currentPicker = pickerResult?.runId === run?.id ? pickerResult : null;
  const isPicking = pickingRunId === run?.id;

  useEffect(() => {
    if (!run || currentPicker?.outcome !== "selected" || !currentPicker.levelPath) return;
    const key = `${run.id}:${currentPicker.levelPath}`;
    if (autoPlayKeyRef.current === key) return;
    autoPlayKeyRef.current = key;
    setStartingAction("picker");
    void onPlay(run.id, currentPicker.levelPath).then((started) => {
      if (!started) setStartingAction(null);
    });
  }, [currentPicker, onPlay, run]);

  useEffect(() => {
    if (!run || !startingAction || replayStatus.runId !== run.id) return;
    if (replayStatus.state === "error" || replayStatus.state === "cancelled") {
      setStartingAction(null);
      return;
    }
    if (
      replayStatus.state === "waiting_for_focus" ||
      replayStatus.state === "starting" ||
      replayStatus.state === "playing"
    ) {
      setStartingAction(null);
      onResetPicker();
      onClose();
    }
  }, [onClose, onResetPicker, replayStatus, run, startingAction]);

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
    const started = await onPlay(run.id);
    if (!started) setStartingAction(null);
  };

  const chooseAnother = async () => {
    if (!run || isPicking || startingAction) return;
    autoPlayKeyRef.current = "";
    await onChooseAnother(run.id);
  };

  const currentPlayError =
    playErrorRunId === run?.id
      ? playError
      : replayStatus.runId === run?.id && replayStatus.state === "error"
        ? translatedDomainError(replayStatus.errorCode ?? "") ||
          replayStatus.message ||
          replayStatus.errorCode ||
          t("dialog.failed")
        : "";
  const pickerMessage =
    currentPlayError ||
    (currentPicker?.outcome === "mismatch" || currentPicker?.outcome === "error"
      ? currentPicker.message
      : null);
  const pickerFailed =
    Boolean(currentPlayError) ||
    currentPicker?.outcome === "mismatch" ||
    currentPicker?.outcome === "error";
  const busy = isPicking || startingAction !== null;
  const originalBusy = startingAction === "original";
  const pickerBusy = isPicking || startingAction === "picker";

  return (
    <Dialog open={Boolean(run)} onOpenChange={(open) => !open && close()}>
      <DialogContent
        aria-describedby="replay-level-choice-description"
        className="min-h-0 w-[min(27rem,calc(100vw-2rem))] overflow-hidden p-0"
        onEscapeKeyDown={(event) => isPicking && event.preventDefault()}
        onPointerDownOutside={(event) => isPicking && event.preventDefault()}
      >
        <div className="px-5 pb-4 pt-5">
          <DialogTitle>{t("dialog.title")}</DialogTitle>
          <p
            id="replay-level-choice-description"
            className="mt-1.5 text-sm leading-relaxed text-muted-foreground"
          >
            {t("dialog.description")}
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
              <span className="block text-sm font-medium">{t("dialog.original")}</span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                {originalBusy ? t("dialog.starting") : t("dialog.useRecorded")}
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
              <span className="text-xs font-semibold uppercase tracking-wide text-primary">
                {t("dialog.default")}
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
              <span className="block text-sm font-medium">{t("dialog.chooseMatching")}</span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                {isPicking
                  ? t("dialog.choosingAndVerifying")
                  : startingAction === "picker"
                    ? currentPicker?.outcome === "selected"
                      ? t("dialog.starting")
                      : t("dialog.openingPicker")
                    : t("dialog.useAnother")}
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
            {commonT("actions.cancel")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
