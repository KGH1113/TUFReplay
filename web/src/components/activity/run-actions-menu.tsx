import {
  ArrowLeft01Icon,
  Delete02Icon,
  FloppyDiskIcon,
  Loading03Icon,
  Mic01Icon,
  MoreVerticalIcon,
  Upload04Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { TFunction } from "i18next";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { ActivityRun } from "@/models/activity/activity-model";
import { formatFileSize } from "@/models/activity/file-size";
import { Button } from "@/shared/ui/button";
import { Dialog, DialogContent, DialogTitle } from "@/shared/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "@/shared/ui/dropdown-menu";

type PendingAction = "keep" | "delete-recording" | "delete-run" | null;

export function RunActionsMenu({
  run,
  disabled,
  runDeleteDisabled,
  onKeepMicrophoneRecording,
  onDeleteMicrophoneRecording,
  onDeleteRun,
}: {
  run: ActivityRun;
  disabled: boolean;
  runDeleteDisabled: boolean;
  onKeepMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onDeleteMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onDeleteRun: (run: ActivityRun) => Promise<void>;
}) {
  const { t } = useTranslation("replay");
  const { t: activityT } = useTranslation("activity");
  const { t: microphoneT, i18n } = useTranslation("microphone");
  const locale = i18n.resolvedLanguage ?? "en";
  const [menuOpen, setMenuOpen] = useState(false);
  const [recordingDialogOpen, setRecordingDialogOpen] = useState(false);
  const [runDialogOpen, setRunDialogOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const [menuError, setMenuError] = useState("");
  const [dialogError, setDialogError] = useState("");
  const busy = pendingAction !== null;

  const keepRecording = async () => {
    if (disabled || busy) return;
    setPendingAction("keep");
    setMenuError("");
    try {
      await onKeepMicrophoneRecording(run);
    } catch (cause) {
      setMenuError(cause instanceof Error ? cause.message : microphoneT("recording.keepFailed"));
    } finally {
      setPendingAction(null);
    }
  };

  const deleteRecording = async () => {
    if (disabled || busy) return;
    setPendingAction("delete-recording");
    setDialogError("");
    try {
      await onDeleteMicrophoneRecording(run);
      setRecordingDialogOpen(false);
    } catch (cause) {
      setDialogError(
        cause instanceof Error ? cause.message : microphoneT("recording.deleteFailed"),
      );
    } finally {
      setPendingAction(null);
    }
  };

  const deleteRun = async () => {
    if (disabled || runDeleteDisabled || busy) return;
    setPendingAction("delete-run");
    setDialogError("");
    try {
      await onDeleteRun(run);
      setRunDialogOpen(false);
    } catch (cause) {
      setDialogError(cause instanceof Error ? cause.message : t("run.deleteFailed"));
    } finally {
      setPendingAction(null);
    }
  };

  return (
    <>
      <DropdownMenu
        open={menuOpen}
        onOpenChange={(open) => {
          setMenuOpen(open);
          if (open) setMenuError("");
        }}
      >
        <DropdownMenuTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={activityT("run.openActions", { runIndex: run.runIndex })}
          >
            <HugeiconsIcon aria-hidden="true" icon={MoreVerticalIcon} className="size-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-fit min-w-0 whitespace-nowrap">
          {run.hasMicrophoneRecording ? (
            <DropdownMenuSub>
              <DropdownMenuSubTrigger>
                <HugeiconsIcon aria-hidden="true" icon={ArrowLeft01Icon} className="size-4" />
                <HugeiconsIcon aria-hidden="true" icon={Mic01Icon} className="size-4" />
                <span>{microphoneT("recording.label")}</span>
              </DropdownMenuSubTrigger>
              <DropdownMenuSubContent className="w-fit min-w-0 whitespace-nowrap">
                <div className="px-2 py-1.5">
                  <p className="text-xs font-medium">
                    {formatFileSize(run.microphoneRecordingBytes, locale)}
                  </p>
                  <p className="mt-0.5 text-[11px] text-muted-foreground">
                    {run.microphoneRecordingPermanent
                      ? microphoneT("recording.permanent")
                      : formatRemaining(run.microphoneRecordingExpiresAtUtc, microphoneT)}
                  </p>
                </div>
                <DropdownMenuSeparator />
                {!run.microphoneRecordingPermanent ? (
                  <DropdownMenuItem
                    disabled={disabled || busy}
                    onSelect={(event) => {
                      event.preventDefault();
                      void keepRecording();
                    }}
                  >
                    <HugeiconsIcon
                      aria-hidden="true"
                      icon={pendingAction === "keep" ? Loading03Icon : FloppyDiskIcon}
                      className={pendingAction === "keep" ? "size-4 animate-spin" : "size-4"}
                    />
                    {microphoneT("recording.keep")}
                  </DropdownMenuItem>
                ) : null}
                <DropdownMenuItem
                  className="text-destructive data-highlighted:bg-destructive/10 data-highlighted:text-destructive"
                  disabled={disabled || busy}
                  onSelect={() => {
                    setDialogError("");
                    setRecordingDialogOpen(true);
                  }}
                >
                  <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
                  {microphoneT("recording.delete")}
                </DropdownMenuItem>
                {menuError ? (
                  <p aria-live="polite" className="px-2 py-1 text-xs text-destructive">
                    {menuError}
                  </p>
                ) : null}
              </DropdownMenuSubContent>
            </DropdownMenuSub>
          ) : (
            <DropdownMenuItem disabled>
              <span aria-hidden="true" className="size-4" />
              <HugeiconsIcon aria-hidden="true" icon={Mic01Icon} className="size-4" />
              {microphoneT("recording.label")}
            </DropdownMenuItem>
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem disabled>
            <span aria-hidden="true" className="size-4" />
            <HugeiconsIcon aria-hidden="true" icon={Upload04Icon} className="size-4" />
            {t("run.submit")}
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            className="text-destructive data-highlighted:bg-destructive/10 data-highlighted:text-destructive"
            disabled={disabled || runDeleteDisabled || busy}
            onSelect={() => {
              setDialogError("");
              setRunDialogOpen(true);
            }}
          >
            <span aria-hidden="true" className="size-4" />
            <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
            {t("run.delete")}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <ConfirmDeleteDialog
        open={recordingDialogOpen}
        title={microphoneT("recording.deleteTitle")}
        description={microphoneT("recording.deleteDescription", {
          size: formatFileSize(run.microphoneRecordingBytes, locale),
          runIndex: run.runIndex,
        })}
        actionLabel={microphoneT("recording.delete")}
        pending={pendingAction === "delete-recording"}
        error={dialogError}
        onOpenChange={setRecordingDialogOpen}
        onConfirm={() => void deleteRecording()}
      />
      <ConfirmDeleteDialog
        open={runDialogOpen}
        title={t("run.deleteTitle")}
        description={t("run.deleteDescription", { runIndex: run.runIndex })}
        actionLabel={t("run.delete")}
        pending={pendingAction === "delete-run"}
        error={dialogError}
        onOpenChange={setRunDialogOpen}
        onConfirm={() => void deleteRun()}
      />
    </>
  );
}

function ConfirmDeleteDialog({
  open,
  title,
  description,
  actionLabel,
  pending,
  error,
  onOpenChange,
  onConfirm,
}: {
  open: boolean;
  title: string;
  description: string;
  actionLabel: string;
  pending: boolean;
  error: string;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}) {
  const { t: commonT } = useTranslation("common");
  const { t } = useTranslation("replay");
  return (
    <Dialog open={open} onOpenChange={(nextOpen) => !pending && onOpenChange(nextOpen)}>
      <DialogContent
        className="min-h-0 w-[min(25rem,calc(100vw-2rem))]"
        onEscapeKeyDown={(event) => pending && event.preventDefault()}
        onPointerDownOutside={(event) => pending && event.preventDefault()}
      >
        <DialogTitle>{title}</DialogTitle>
        <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{description}</p>
        {error ? (
          <p aria-live="polite" className="mt-3 text-sm text-destructive">
            {error}
          </p>
        ) : null}
        <div className="mt-6 flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={pending}
            onClick={() => onOpenChange(false)}
          >
            {commonT("actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            disabled={pending}
            onClick={onConfirm}
          >
            {pending ? t("run.deleting") : actionLabel}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function formatRemaining(value: string | null, t: TFunction<"microphone">) {
  if (!value) return t("recording.deletesSoon");
  const expiresAt = Date.parse(value);
  if (Number.isNaN(expiresAt)) return t("recording.deletesSoon");
  const remaining = expiresAt - Date.now();
  if (remaining <= 0) return t("recording.deletesSoon");
  const hours = Math.ceil(remaining / 3_600_000);
  if (hours < 24) return t("recording.deletesInHours", { count: hours });
  return t("recording.deletesInDays", { count: Math.ceil(hours / 24) });
}
