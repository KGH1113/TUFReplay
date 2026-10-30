import { Cancel01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { RunRecordingPanel } from "@/components/activity/run-recording-panel";
import { formatTimeWithOffsetParts } from "@/models/activity/activity-date";
import type { ActivityRun } from "@/models/activity/activity-model";
import type { RunRecordingActions } from "@/models/activity/run-recordings";
import { Button } from "@/shared/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/shared/ui/dialog";

/** A run's microphone audio and camera video, each with export, keep and delete. */
export function RunRecordingsDialog({
  run,
  open,
  readOnly,
  replayActive,
  timeZone,
  actions,
  onOpenChange,
}: {
  run: ActivityRun | null;
  open: boolean;
  readOnly: boolean;
  replayActive: boolean;
  timeZone: string;
  actions: RunRecordingActions;
  onOpenChange: (open: boolean) => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const started = run
    ? formatTimeWithOffsetParts(
        run.startedAtUtc,
        i18n.resolvedLanguage ?? "en",
        t("time.open"),
        timeZone,
      )
    : null;
  const hasRecordings = Boolean(run?.hasMicrophoneRecording || run?.hasCameraRecording);

  return (
    <Dialog open={open && run !== null} onOpenChange={onOpenChange}>
      <DialogContent className="min-h-0 w-[min(28rem,calc(100vw-2rem))] p-5">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <DialogTitle className="text-lg font-semibold tracking-tight">
              {t("recordings.title")}
            </DialogTitle>
            <DialogDescription className="mt-0.5 text-xs tabular-nums">
              {run && started
                ? t("recordings.subtitle", {
                    runIndex: run.runIndex,
                    time: started.time,
                    offset: started.offset,
                  })
                : ""}
            </DialogDescription>
          </div>
          <Button
            type="button"
            size="icon-sm"
            variant="ghost"
            className="-mt-1 -mr-1.5 text-muted-foreground"
            aria-label={t("recordings.close")}
            onClick={() => onOpenChange(false)}
          >
            <HugeiconsIcon aria-hidden="true" icon={Cancel01Icon} className="size-4" />
          </Button>
        </div>
        <div className="mt-4 grid gap-2.5">
          {run && hasRecordings ? (
            <>
              <RunRecordingPanel
                run={run}
                kind="microphone"
                disabled={readOnly}
                replayActive={replayActive}
                actions={actions.microphone}
              />
              <RunRecordingPanel
                run={run}
                kind="camera"
                disabled={readOnly}
                replayActive={replayActive}
                actions={actions.camera}
              />
            </>
          ) : (
            <p role="status" className="py-4 text-sm leading-relaxed text-muted-foreground">
              {t("recordings.empty")}
            </p>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
