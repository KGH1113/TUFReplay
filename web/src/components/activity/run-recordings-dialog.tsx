import {
  Cancel01Icon,
  Delete02Icon,
  Download01Icon,
  Infinity01Icon,
  Loading03Icon,
  Tick02Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { type ReactNode, useId, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useRunRecordings } from "@/hooks/activity/use-run-recordings";
import type { ActivityRun } from "@/models/activity/activity-model";
import { formatFileSize } from "@/models/activity/file-size";
import {
  type RecordingAction,
  type RecordingKind,
  type RunRecordingActions,
  recordingTimeRemaining,
  runRecording,
} from "@/models/activity/run-recordings";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/shared/ui/tabs";

export function RunRecordingsDialog({
  run,
  open,
  disabled,
  deletionDisabled,
  actions,
  onOpenChange,
  restoreFocus,
}: {
  run: ActivityRun;
  open: boolean;
  disabled: boolean;
  deletionDisabled: boolean;
  actions: RunRecordingActions;
  onOpenChange: (open: boolean) => void;
  restoreFocus: () => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const dialogSurface = useRef<HTMLDivElement>(null);
  const deleteDescriptionId = useId();
  const control = useRunRecordings(
    run,
    open,
    disabled,
    deletionDisabled,
    actions,
    (kind: RecordingKind, action: RecordingAction) => t(`recordings.${kind}.${action}Failed`),
  );
  const size = formatFileSize(control.recording.bytes, i18n.resolvedLanguage ?? "en");
  const remaining = recordingTimeRemaining(control.recording.expiresAtUtc);
  const retention = control.recording.permanent
    ? t("recordings.permanent")
    : remaining.unit === "soon"
      ? t("recordings.deletesSoon")
      : t(remaining.unit === "hours" ? "recordings.remainingHours" : "recordings.remainingDays", {
          count: remaining.count,
        });

  return (
    <Dialog open={open} onOpenChange={(next) => !control.busy && onOpenChange(next)}>
      <DialogContent
        ref={dialogSurface}
        tabIndex={-1}
        className="flex h-[min(25rem,calc(100svh-2rem))] min-h-0 flex-col gap-4 overflow-hidden"
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          dialogSurface.current?.focus({ preventScroll: true });
        }}
        onEscapeKeyDown={(event) => control.busy && event.preventDefault()}
        onPointerDownOutside={(event) => control.busy && event.preventDefault()}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          restoreFocus();
        }}
      >
        <DialogHeader className="flex shrink-0 flex-row items-center justify-between gap-4 space-y-0">
          <DialogTitle className="text-balance">{t("recordings.title")}</DialogTitle>
          <DialogDescription className="sr-only">{t("recordings.description")}</DialogDescription>
          <Button
            ref={control.closeButton}
            type="button"
            size="icon-sm"
            variant="ghost"
            aria-label={t("recordings.close")}
            className="text-muted-foreground transition-[color,background-color,box-shadow] hover:text-foreground motion-reduce:transition-none"
            disabled={control.busy}
            onClick={() => onOpenChange(false)}
          >
            <HugeiconsIcon aria-hidden="true" icon={Cancel01Icon} className="size-4" />
          </Button>
        </DialogHeader>
        <Tabs
          value={control.kind}
          onValueChange={control.selectKind}
          className="min-h-0 flex-1 gap-4"
        >
          <TabsList aria-label={t("recordings.label")} className="shrink-0">
            {(["microphone", "camera"] as const).map((kind) => (
              <TabsTrigger
                key={kind}
                ref={kind === control.kind ? control.activeTab : undefined}
                value={kind}
                disabled={control.busy || !runRecording(run, kind).available}
                className="px-4 transition-[color,background-color,box-shadow] data-active:text-primary data-active:hover:text-primary dark:data-active:text-primary dark:data-active:hover:text-primary motion-reduce:transition-none"
              >
                {t(`recordings.${kind}.label`)}
              </TabsTrigger>
            ))}
          </TabsList>
          <TabsContent
            value={control.kind}
            aria-busy={control.busy}
            className="-mx-1 min-h-0 overflow-x-hidden overflow-y-auto overscroll-contain px-1 focus-visible:rounded-lg focus-visible:ring-2 focus-visible:ring-ring/50"
          >
            {control.recording.available ? (
              control.confirmDelete ? (
                <div className="space-y-4 py-4">
                  <div className="space-y-2">
                    <h3 className="text-sm font-medium text-destructive">
                      {t(`recordings.${control.kind}.deleteTitle`)}
                    </h3>
                    <p
                      id={deleteDescriptionId}
                      className="text-sm leading-relaxed text-muted-foreground"
                    >
                      {t(`recordings.${control.kind}.deleteDescription`, { size })}
                    </p>
                  </div>
                  <div className="flex flex-wrap justify-end gap-2">
                    <Button
                      ref={control.deleteCancel}
                      type="button"
                      aria-describedby={deleteDescriptionId}
                      size="sm"
                      variant="outline"
                      className="transition-[color,background-color,box-shadow] motion-reduce:transition-none"
                      disabled={control.busy}
                      onClick={() => control.setConfirmDelete(false)}
                    >
                      {t("recordings.cancel")}
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="destructive"
                      aria-describedby={deleteDescriptionId}
                      className="gap-2 transition-[color,background-color,box-shadow] motion-reduce:transition-none"
                      disabled={disabled || control.busy || deletionDisabled}
                      onClick={() => void control.act("delete")}
                    >
                      <HugeiconsIcon
                        aria-hidden="true"
                        icon={control.pending === "delete" ? Loading03Icon : Delete02Icon}
                        className={
                          control.pending === "delete"
                            ? "size-4 animate-spin motion-reduce:animate-none"
                            : "size-4"
                        }
                      />
                      {t(
                        control.pending === "delete"
                          ? "recordings.deleting"
                          : `recordings.${control.kind}.delete`,
                      )}
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="divide-y divide-border/60">
                  <RecordingActionRow
                    title={t("recordings.exportTitle")}
                    description={
                      <span className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
                        <span>{t("recordings.size")}</span>
                        <span className="text-sm font-semibold tabular-nums text-foreground">
                          {size}
                        </span>
                        <span>· {control.kind === "camera" ? "MP4" : "WAV"}</span>
                      </span>
                    }
                  >
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      aria-label={t(
                        control.pending === "download"
                          ? "recordings.downloading"
                          : `recordings.${control.kind}.download`,
                      )}
                      className="gap-2 transition-[color,background-color,box-shadow] motion-reduce:transition-none"
                      disabled={disabled || control.busy}
                      onClick={() => void control.act("download")}
                    >
                      <HugeiconsIcon
                        aria-hidden="true"
                        icon={control.pending === "download" ? Loading03Icon : Download01Icon}
                        className={
                          control.pending === "download"
                            ? "size-4 animate-spin motion-reduce:animate-none"
                            : "size-4"
                        }
                      />
                      {t(
                        control.pending === "download"
                          ? "recordings.downloading"
                          : "recordings.exportAction",
                      )}
                    </Button>
                  </RecordingActionRow>
                  <RecordingActionRow
                    title={t("recordings.retention")}
                    description={
                      <span
                        ref={control.keepStatus}
                        role="status"
                        tabIndex={-1}
                        className={`rounded-sm text-sm font-medium outline-none focus-visible:ring-2 focus-visible:ring-ring/50 ${control.recording.permanent ? "text-primary" : remaining.unit === "soon" ? "text-destructive" : "text-foreground"}`}
                      >
                        {retention}
                      </span>
                    }
                  >
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      className="gap-2 transition-[color,background-color,box-shadow] motion-reduce:transition-none"
                      disabled={disabled || control.busy || control.recording.permanent}
                      onClick={() => void control.act("keep")}
                    >
                      <HugeiconsIcon
                        aria-hidden="true"
                        icon={
                          control.recording.permanent
                            ? Tick02Icon
                            : control.pending === "keep"
                              ? Loading03Icon
                              : Infinity01Icon
                        }
                        className={
                          control.pending === "keep"
                            ? "size-4 animate-spin motion-reduce:animate-none"
                            : "size-4"
                        }
                      />
                      {t(
                        control.recording.permanent
                          ? "recordings.kept"
                          : control.pending === "keep"
                            ? "recordings.keeping"
                            : "recordings.keep",
                      )}
                    </Button>
                  </RecordingActionRow>
                  <RecordingActionRow
                    title={t(`recordings.${control.kind}.delete`)}
                    description={t(
                      deletionDisabled
                        ? "recordings.replayLocked"
                        : `recordings.${control.kind}.deleteHint`,
                    )}
                  >
                    <Button
                      ref={control.deleteTrigger}
                      type="button"
                      size="sm"
                      variant="destructive"
                      aria-label={t(`recordings.${control.kind}.delete`)}
                      className="gap-2 transition-[color,background-color,box-shadow] motion-reduce:transition-none"
                      disabled={disabled || control.busy || deletionDisabled}
                      onClick={() => control.setConfirmDelete(true)}
                    >
                      <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
                      {t("recordings.deleteAction")}
                    </Button>
                  </RecordingActionRow>
                </div>
              )
            ) : (
              <p role="status" className="py-4 text-sm leading-relaxed text-muted-foreground">
                {t("recordings.empty")}
              </p>
            )}
            {control.error ? (
              <p role="alert" className="mt-3 text-sm leading-relaxed text-destructive">
                {control.error}
              </p>
            ) : null}
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}

function RecordingActionRow({
  title,
  description,
  children,
}: {
  title: string;
  description: ReactNode;
  children: ReactNode;
}) {
  return (
    <section aria-label={title} className="flex min-h-20 items-center justify-between gap-4 py-4">
      <div className="min-w-0 space-y-1">
        <h3 className="text-sm font-medium">{title}</h3>
        <div className="text-xs leading-relaxed text-muted-foreground">{description}</div>
      </div>
      {children}
    </section>
  );
}
