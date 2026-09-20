import {
  ArrowRight02Icon,
  Loading03Icon,
  MusicNote01Icon,
  PlayIcon,
  Tick02Icon,
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
  const replayRunId = replayStatus.runId;
  const replayState = replayStatus.state;

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
    if (!run || !startingAction || replayRunId !== run.id) return;
    if (replayState === "error" || replayState === "cancelled") {
      setStartingAction(null);
      return;
    }
    if (replayState !== "playing" && replayState !== "completed") return;

    const timeout = setTimeout(() => {
      setStartingAction(null);
      onResetPicker();
      onClose();
    }, 1_200);
    return () => clearTimeout(timeout);
  }, [onClose, onResetPicker, replayRunId, replayState, run, startingAction]);

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
      ? translatedDomainError(currentPicker.errorCode ?? "") || currentPicker.message
      : null);
  const pickerFailed =
    Boolean(currentPlayError) ||
    currentPicker?.outcome === "mismatch" ||
    currentPicker?.outcome === "error";
  const busy = isPicking || startingAction !== null;
  const originalBusy = startingAction === "original";
  const pickerBusy = isPicking || startingAction === "picker";
  const handoff = startingAction ? replayHandoff(replayStatus, run?.id ?? null) : null;

  return (
    <Dialog open={Boolean(run)} onOpenChange={(open) => !open && close()}>
      <DialogContent
        aria-describedby="replay-level-choice-description"
        className="min-h-0 w-[min(27rem,calc(100vw-2rem))] overflow-hidden p-0"
        onEscapeKeyDown={(event) => busy && event.preventDefault()}
        onPointerDownOutside={(event) => busy && event.preventDefault()}
      >
        <div className="px-5 pb-4 pt-5">
          <DialogTitle>{handoff ? t(`handoff.${handoff}.title`) : t("dialog.title")}</DialogTitle>
          <p
            id="replay-level-choice-description"
            className="mt-1.5 text-sm leading-relaxed text-muted-foreground"
          >
            {handoff ? t(`handoff.${handoff}.description`) : t("dialog.description")}
          </p>
        </div>

        {handoff ? (
          <div
            role="status"
            aria-live="polite"
            className="border-y border-border px-5 py-6 text-center"
          >
            <div className="mx-auto grid size-14 place-items-center rounded-full bg-primary/10 text-primary ring-1 ring-primary/20 motion-safe:animate-in motion-safe:zoom-in-95 motion-safe:duration-300">
              {handoff === "sending" || handoff === "starting" ? (
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={Loading03Icon}
                  className="size-6 animate-spin"
                  strokeWidth={2.2}
                />
              ) : (
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={Tick02Icon}
                  className="size-6"
                  strokeWidth={2.6}
                />
              )}
            </div>
            {handoff === "delivered" || handoff === "waitingForFocus" ? (
              <div className="mt-5 flex items-center justify-center gap-2 text-xs text-muted-foreground">
                <kbd className="rounded-md bg-muted px-2 py-1 font-sans font-medium text-foreground ring-1 ring-foreground/10">
                  Alt Tab
                </kbd>
                <span>{t("handoff.or")}</span>
                <kbd className="rounded-md bg-muted px-2 py-1 font-sans font-medium text-foreground ring-1 ring-foreground/10">
                  ⌘ Tab
                </kbd>
              </div>
            ) : null}
          </div>
        ) : (
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
        )}

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

        {!handoff ? (
          <div className="flex justify-end px-4 py-3">
            <Button type="button" variant="ghost" size="sm" disabled={busy} onClick={close}>
              {commonT("actions.cancel")}
            </Button>
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

type ReplayHandoffStep = "sending" | "delivered" | "waitingForFocus" | "starting" | "playing";

function replayHandoff(status: ReplayStatus, runId: string | null): ReplayHandoffStep {
  if (!runId || status.runId !== runId || !status.operationId) return "sending";
  if (status.state === "waiting_for_focus") return "waitingForFocus";
  if (status.state === "starting") return "starting";
  if (status.state === "playing" || status.state === "completed") return "playing";
  return "delivered";
}
