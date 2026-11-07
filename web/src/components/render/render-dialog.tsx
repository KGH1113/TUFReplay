import { useState } from "react";
import { useTranslation } from "react-i18next";
import { RenderFailureNotice, RenderNotice } from "@/components/render/render-notice";
import { RenderOptionsForm, RenderSaveLocation } from "@/components/render/render-options-form";
import { RenderProgress } from "@/components/render/render-progress";
import { RenderResult } from "@/components/render/render-result";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import type { ActivityRun, LevelMetadata } from "@/models/activity/activity-model";
import { describeLevel } from "@/models/activity/level-description";
import { runEndPercent, runOutcome, runStartPercent } from "@/models/activity/run-progress";
import { formatXAccuracy } from "@/models/activity/x-accuracy";
import { describeFolder } from "@/models/render/render-choices";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { ScrollArea } from "@/shared/ui/scroll-area";

type Control = ReturnType<typeof useRenderControl>;

export function RenderDialog({
  control,
  metadata,
}: {
  control: Control;
  /** The level the run belongs to, named in the header. */
  metadata?: LevelMetadata;
}) {
  const { t } = useTranslation("render");
  const [opening, setOpening] = useState(false);
  const [openFailed, setOpenFailed] = useState(false);
  const done = control.phase === "completed";
  const setup = !control.busy && !done;
  const unavailable = control.health ? !control.health.orbitAvailable : false;
  const canStart =
    !control.busy &&
    control.phase !== "checking" &&
    !unavailable &&
    !control.health?.busy &&
    Boolean(control.health?.orbitAvailable) &&
    control.optionValidation.success;

  const openOutput = async (reveal: boolean) => {
    const jobId = control.job?.jobId;
    if (!jobId) return;
    setOpening(true);
    setOpenFailed(false);
    try {
      if (!(await control.openRenderOutput(jobId, reveal))) setOpenFailed(true);
    } catch {
      setOpenFailed(true);
    } finally {
      setOpening(false);
    }
  };

  return (
    <Dialog
      open={Boolean(control.run)}
      onOpenChange={(open) => {
        if (!open && !control.busy) {
          setOpenFailed(false);
          control.close();
        }
      }}
    >
      <DialogContent
        className={cn(
          "flex max-h-[min(92dvh,56rem)] flex-col gap-0 p-0",
          // Settings take two columns and the finished video wants a large picture; progress needs less room.
          setup || done ? "w-[min(60rem,calc(100vw-2rem))]" : "w-[min(36rem,calc(100vw-2rem))]",
        )}
        // Nothing is focused on open, so no field shows a selected value or a ring.
        onOpenAutoFocus={(event) => event.preventDefault()}
        onEscapeKeyDown={(event) => {
          // Esc in a full-screen preview only leaves full screen.
          if (control.busy || document.fullscreenElement) event.preventDefault();
        }}
        onInteractOutside={(event) => {
          if (control.busy) event.preventDefault();
        }}
      >
        <DialogHeader className="shrink-0 gap-1 px-6 pt-6 pb-4">
          <DialogTitle>{t(done ? "doneTitle" : "title")}</DialogTitle>
          <DialogDescription className="truncate">
            {control.run ? (
              <RunSummary run={control.run} metadata={metadata} levelPath={control.levelPath} />
            ) : null}
          </DialogDescription>
        </DialogHeader>
        {/* Radix lays the content out as a table that grows with long text; keep it to the dialog's width. */}
        <ScrollArea className="min-h-0 [&_[data-radix-scroll-area-viewport]]:max-h-[calc(min(92dvh,56rem)-9.5rem)] [&_[data-radix-scroll-area-viewport]>div]:block!">
          <div className="space-y-5 px-6 pb-6">
            {control.busy ? (
              <RenderProgress phase={control.phase} progress={control.progress} job={control.job} />
            ) : done && control.job ? (
              <RenderResult control={control} openFailed={openFailed} />
            ) : (
              <>
                <SetupNotices control={control} unavailable={unavailable} />
                <RenderOptionsForm control={control} />
              </>
            )}
          </div>
        </ScrollArea>
        <div className="flex shrink-0 flex-wrap items-center justify-end gap-x-4 gap-y-2 border-t border-border bg-muted/20 px-6 py-4">
          {setup ? <RenderSaveLocation control={control} /> : null}
          <div className="flex shrink-0 gap-2">
            {control.busy ? (
              <Button variant="outline" onClick={() => void control.cancel()}>
                {t("stop")}
              </Button>
            ) : done ? (
              <>
                <Button variant="ghost" onClick={control.close}>
                  {t("close")}
                </Button>
                <Button variant="outline" disabled={opening} onClick={() => void openOutput(true)}>
                  {t("result.reveal")}
                </Button>
                <Button disabled={opening} onClick={() => void openOutput(false)}>
                  {t("result.open")}
                </Button>
              </>
            ) : (
              <>
                <Button variant="ghost" onClick={control.close}>
                  {t("close")}
                </Button>
                <Button disabled={!canStart} onClick={() => void control.start()}>
                  {control.phase === "checking"
                    ? t("checking")
                    : control.phase === "failed" || control.phase === "cancelled"
                      ? t("retry")
                      : t("start")}
                </Button>
              </>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

/**
 * What is being made: the level, the file chosen in place of the recorded one, how far the run
 * got and its accuracy.
 */
function RunSummary({
  run,
  metadata,
  levelPath,
}: {
  run: ActivityRun;
  metadata?: LevelMetadata;
  levelPath?: string;
}) {
  const { t, i18n } = useTranslation("render");
  const { t: activityT } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const start = runStartPercent(run);
  const end = runEndPercent(run);
  const parts = [
    metadata ? describeLevel(metadata, activityT).name : null,
    levelPath ? describeFolder(levelPath).name : null,
    runOutcome(run) === "clear"
      ? t("summary.clear")
      : start > 0
        ? t("summary.range", { start, end })
        : t("summary.reached", { end }),
    run.xAccuracy === null ? null : formatXAccuracy(run.xAccuracy, locale),
  ];
  return <>{parts.filter(Boolean).join(" · ")}</>;
}

/** Why the video cannot be made right now, or how the last try ended. */
function SetupNotices({ control, unavailable }: { control: Control; unavailable: boolean }) {
  const { t } = useTranslation("render");
  if (control.health && !control.health.available)
    return <RenderNotice message={t("errors.ipc_unavailable")} />;
  if (unavailable) return <RenderNotice message={t("notice.orbitMissing")} />;
  if (control.health?.busy) return <RenderNotice message={t("notice.busy")} />;
  if (control.phase === "cancelled") return <RenderNotice message={t("notice.stopped")} />;
  if (control.phase === "failed")
    return (
      <RenderFailureNotice
        code={control.errorCode}
        message={control.errorMessage}
        details={control.errorDetails}
      />
    );
  return null;
}
