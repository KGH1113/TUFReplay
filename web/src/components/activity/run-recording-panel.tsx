import {
  Delete02Icon,
  Download01Icon,
  Infinity01Icon,
  Loading03Icon,
  Mic01Icon,
  Tick02Icon,
  Video01Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useId } from "react";
import { useTranslation } from "react-i18next";
import { useRunRecordingControl } from "@/hooks/activity/use-run-recording-control";
import type { ActivityRun } from "@/models/activity/activity-model";
import { formatFileSize } from "@/models/activity/file-size";
import { formatDuration } from "@/models/activity/run-progress";
import {
  type RecordingAction,
  type RecordingKind,
  type RunRecordingAction,
  recordingTimeRemaining,
} from "@/models/activity/run-recordings";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";

export function RunRecordingPanel({
  run,
  kind,
  disabled,
  replayActive,
  actions,
}: {
  run: ActivityRun;
  kind: RecordingKind;
  disabled: boolean;
  replayActive: boolean;
  actions: Record<RecordingAction, RunRecordingAction>;
}) {
  const { t, i18n } = useTranslation("activity");
  const { t: commonT } = useTranslation("common");
  const locale = i18n.resolvedLanguage ?? "en";
  const descriptionId = useId();
  const deletionDisabled = run.locked || replayActive;
  const control = useRunRecordingControl({
    run,
    kind,
    disabled,
    deletionDisabled,
    actions,
    failureMessage: (action) => t(`recordings.${kind}.${action}Failed`),
  });
  const { recording } = control;
  if (!recording.available) return null;

  const size = formatFileSize(recording.bytes, locale);
  const remaining = recordingTimeRemaining(recording.expiresAtUtc);
  const retention = recording.permanent
    ? t("recordings.permanent")
    : remaining.unit === "soon"
      ? t("recordings.deletesSoon")
      : t(remaining.unit === "hours" ? "recordings.remainingHours" : "recordings.remainingDays", {
          count: remaining.count,
        });
  const details = [
    size,
    kind === "camera" ? "MP4" : "WAV",
    kind === "microphone" ? formatDuration((run.microphoneDurationSeconds ?? 0) * 1000) : null,
    kind === "microphone" && run.microphoneSampleRate
      ? commonT("units.hertz", { value: run.microphoneSampleRate.toLocaleString(locale) })
      : null,
    kind === "microphone" && run.microphoneChannels
      ? t(run.microphoneChannels === 1 ? "recordings.mono" : "recordings.channels", {
          count: run.microphoneChannels,
        })
      : null,
  ].filter((part): part is string => Boolean(part) && part !== "0:00");
  const deleteHint = run.locked
    ? t("lock.recordingDeleteBlocked")
    : replayActive
      ? t("recordings.replayLocked")
      : null;

  return (
    <section
      aria-labelledby={`${descriptionId}-title`}
      aria-busy={control.busy}
      className="rounded-xl border border-border bg-background/40 p-3"
    >
      <div className="flex items-start gap-3">
        <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-foreground/[0.06] text-muted-foreground">
          <HugeiconsIcon
            aria-hidden="true"
            icon={kind === "camera" ? Video01Icon : Mic01Icon}
            size={16}
            strokeWidth={1.8}
          />
        </span>
        <div className="min-w-0 flex-1">
          <h4 id={`${descriptionId}-title`} className="text-sm font-medium">
            {t(`recordings.${kind}.name`)}
          </h4>
          <p className="mt-0.5 truncate text-xs tabular-nums text-muted-foreground">
            {details.join(" · ")}
          </p>
          <p
            role="status"
            className={cn(
              "mt-1 flex items-center gap-1 text-xs font-medium",
              recording.permanent
                ? "text-primary"
                : remaining.unit === "soon"
                  ? "text-destructive"
                  : "text-foreground/80",
            )}
          >
            {recording.permanent ? (
              <HugeiconsIcon aria-hidden="true" icon={Infinity01Icon} size={13} strokeWidth={2} />
            ) : null}
            {retention}
          </p>
        </div>
      </div>

      {control.confirmDelete ? (
        <div className="mt-3 rounded-lg bg-destructive/8 p-3">
          <p id={descriptionId} className="text-xs leading-relaxed text-foreground/90">
            {t(`recordings.${kind}.deleteDescription`, { size })}
          </p>
          <div className="mt-3 flex justify-end gap-2">
            <Button
              ref={control.deleteCancel}
              type="button"
              size="sm"
              variant="ghost"
              disabled={control.busy}
              onClick={control.cancelDelete}
            >
              {t("recordings.cancel")}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="destructive"
              aria-describedby={descriptionId}
              disabled={disabled || control.busy || deletionDisabled}
              onClick={() => void control.act("delete")}
            >
              <HugeiconsIcon
                aria-hidden="true"
                icon={control.pending === "delete" ? Loading03Icon : Delete02Icon}
                className={cn(
                  "size-4",
                  control.pending === "delete" && "animate-spin motion-reduce:animate-none",
                )}
              />
              {t(
                control.pending === "delete" ? "recordings.deleting" : `recordings.${kind}.delete`,
              )}
            </Button>
          </div>
        </div>
      ) : (
        <div className="mt-3 flex flex-wrap items-center gap-1.5">
          <Button
            type="button"
            size="sm"
            variant="secondary"
            disabled={disabled || control.busy}
            onClick={() => void control.act("download")}
          >
            <HugeiconsIcon
              aria-hidden="true"
              icon={control.pending === "download" ? Loading03Icon : Download01Icon}
              className={cn(
                "size-4",
                control.pending === "download" && "animate-spin motion-reduce:animate-none",
              )}
            />
            {t(
              control.pending === "download"
                ? "recordings.downloading"
                : `recordings.${kind}.download`,
            )}
          </Button>
          <Button
            type="button"
            size="sm"
            variant="ghost"
            disabled={disabled || control.busy || recording.permanent}
            onClick={() => void control.act("keep")}
          >
            <HugeiconsIcon
              aria-hidden="true"
              icon={
                recording.permanent
                  ? Tick02Icon
                  : control.pending === "keep"
                    ? Loading03Icon
                    : Infinity01Icon
              }
              className={cn(
                "size-4",
                control.pending === "keep" && "animate-spin motion-reduce:animate-none",
              )}
            />
            {t(
              recording.permanent
                ? "recordings.kept"
                : control.pending === "keep"
                  ? "recordings.keeping"
                  : "recordings.keep",
            )}
          </Button>
          <Button
            ref={control.deleteTrigger}
            type="button"
            size="icon-sm"
            variant="ghost"
            className="ml-auto text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            aria-label={t(`recordings.${kind}.delete`)}
            title={deleteHint ?? t(`recordings.${kind}.deleteHint`)}
            disabled={disabled || control.busy || deletionDisabled}
            onClick={control.requestDelete}
          >
            <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
          </Button>
        </div>
      )}
      {deleteHint && !control.confirmDelete ? (
        <p className="mt-2 text-xs leading-relaxed text-subtle-foreground">{deleteHint}</p>
      ) : null}
      {control.error ? (
        <p role="alert" className="mt-2 text-xs leading-relaxed text-destructive">
          {control.error}
        </p>
      ) : null}
    </section>
  );
}
