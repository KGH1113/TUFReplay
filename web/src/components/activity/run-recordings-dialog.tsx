import {
  Delete02Icon,
  Download01Icon,
  FloppyDiskIcon,
  Loading03Icon,
  Mic01Icon,
  Video01Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { useRunRecordings } from "@/hooks/activity/use-run-recordings";
import type { ActivityRun } from "@/models/activity/activity-model";
import { formatFileSize } from "@/models/activity/file-size";
import {
  type RecordingAction,
  type RecordingKind,
  type RunRecordingActions,
  recordingTimeRemaining,
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
      : t(remaining.unit === "hours" ? "recordings.deletesInHours" : "recordings.deletesInDays", {
          count: remaining.count,
        });

  return (
    <Dialog open={open} onOpenChange={(next) => !control.busy && onOpenChange(next)}>
      <DialogContent
        className="flex max-h-[calc(100dvh-2rem)] min-h-0 w-[min(34rem,calc(100vw-2rem))] flex-col gap-6 overflow-y-auto p-5 sm:p-7"
        onEscapeKeyDown={(event) => control.busy && event.preventDefault()}
        onPointerDownOutside={(event) => control.busy && event.preventDefault()}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          restoreFocus();
        }}
      >
        <DialogHeader>
          <DialogTitle>{t("recordings.title", { runIndex: run.runIndex })}</DialogTitle>
          <DialogDescription className="leading-relaxed">
            {t("recordings.description")}
          </DialogDescription>
        </DialogHeader>
        <Tabs value={control.kind} onValueChange={control.selectKind} className="gap-6">
          <TabsList
            variant="line"
            aria-label={t("recordings.label")}
            className="w-full shrink-0 justify-start border-b border-border p-0"
          >
            {(["microphone", "camera"] as const).map((kind) => (
              <TabsTrigger
                key={kind}
                ref={kind === control.kind ? control.activeTab : undefined}
                value={kind}
                disabled={control.busy}
                className="flex-none gap-2 px-4 after:bottom-0"
              >
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={kind === "microphone" ? Mic01Icon : Video01Icon}
                  className="size-4"
                />
                {t(`recordings.${kind}.label`)}
              </TabsTrigger>
            ))}
          </TabsList>
          <TabsContent value={control.kind} className="space-y-5">
            {control.recording.available ? (
              <>
                <div className="space-y-4">
                  <h3 className="text-base font-semibold">
                    {t(`recordings.${control.kind}.name`)}
                  </h3>
                  <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-6 gap-y-2 text-sm">
                    <dt className="text-muted-foreground">{t("recordings.size")}</dt>
                    <dd className="text-right tabular-nums">{size}</dd>
                    <dt className="text-muted-foreground">{t("recordings.retention")}</dt>
                    <dd role="status" className="text-right">
                      {retention}
                    </dd>
                  </dl>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    type="button"
                    size="sm"
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
                        : `recordings.${control.kind}.download`,
                    )}
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    disabled={disabled || control.busy || control.recording.permanent}
                    onClick={() => void control.act("keep")}
                  >
                    <HugeiconsIcon
                      aria-hidden="true"
                      icon={control.pending === "keep" ? Loading03Icon : FloppyDiskIcon}
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
                </div>
                {control.confirmDelete ? (
                  <div className="space-y-3 border-t border-border pt-4">
                    <p className="text-sm font-medium">
                      {t(`recordings.${control.kind}.deleteTitle`)}
                    </p>
                    <p className="text-sm leading-relaxed text-muted-foreground">
                      {t(`recordings.${control.kind}.deleteDescription`, {
                        size,
                        runIndex: run.runIndex,
                      })}
                    </p>
                    <div className="flex justify-end gap-2">
                      <Button
                        ref={control.deleteCancel}
                        type="button"
                        size="sm"
                        variant="outline"
                        disabled={control.busy}
                        onClick={() => control.setConfirmDelete(false)}
                      >
                        {t("recordings.cancel")}
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="destructive"
                        disabled={disabled || control.busy || deletionDisabled}
                        onClick={() => void control.act("delete")}
                      >
                        {t(
                          control.pending === "delete"
                            ? "recordings.deleting"
                            : `recordings.${control.kind}.delete`,
                        )}
                      </Button>
                    </div>
                  </div>
                ) : (
                  <Button
                    ref={control.deleteTrigger}
                    type="button"
                    size="sm"
                    variant="ghost"
                    className="px-0 text-destructive hover:bg-destructive/10 hover:text-destructive"
                    disabled={disabled || control.busy || deletionDisabled}
                    onClick={() => control.setConfirmDelete(true)}
                  >
                    <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
                    {t(`recordings.${control.kind}.delete`)}
                  </Button>
                )}
                {deletionDisabled ? (
                  <p className="text-xs leading-relaxed text-muted-foreground">
                    {t("recordings.replayLocked")}
                  </p>
                ) : null}
              </>
            ) : (
              <p role="status" className="py-6 text-sm leading-relaxed text-muted-foreground">
                {t(`recordings.${control.kind}.empty`)}
              </p>
            )}
            {control.error ? (
              <p role="alert" className="text-sm leading-relaxed text-destructive">
                {control.error}
              </p>
            ) : null}
          </TabsContent>
        </Tabs>
        <div className="flex justify-end border-t border-border pt-4">
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={control.busy}
            onClick={() => onOpenChange(false)}
          >
            {t("recordings.close")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
