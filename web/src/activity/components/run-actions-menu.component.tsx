import {
  ArrowLeft01Icon,
  Delete02Icon,
  Download04Icon,
  FloppyDiskIcon,
  Loading03Icon,
  Mic01Icon,
  MoreVerticalIcon,
  Upload04Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useState } from "react";

import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle } from "@/ui/dialog.component";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "@/ui/dropdown-menu.component";
import type { ActivityRun } from "../activity.model";
import { formatFileSize } from "../lib/file-size.format";

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
      setMenuError(cause instanceof Error ? cause.message : "Could not keep microphone recording");
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
        cause instanceof Error ? cause.message : "Could not delete microphone recording",
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
      setDialogError(cause instanceof Error ? cause.message : "Could not delete run");
    } finally {
      setPendingAction(null);
    }
  };

  return (
    <>
      <DropdownMenu onOpenChange={(open) => open && setMenuError("")}>
        <DropdownMenuTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={`Open actions for run ${run.RunIndex}`}
          >
            <HugeiconsIcon aria-hidden="true" icon={MoreVerticalIcon} className="size-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-fit min-w-0 whitespace-nowrap">
          {run.HasMicrophoneRecording ? (
            <DropdownMenuSub>
              <DropdownMenuSubTrigger>
                <HugeiconsIcon aria-hidden="true" icon={ArrowLeft01Icon} className="size-4" />
                <HugeiconsIcon aria-hidden="true" icon={Mic01Icon} className="size-4" />
                <span>Microphone</span>
              </DropdownMenuSubTrigger>
              <DropdownMenuSubContent className="w-fit min-w-0 whitespace-nowrap">
                <div className="px-2 py-1.5">
                  <p className="text-xs font-medium">
                    {formatFileSize(run.MicrophoneRecordingBytes)}
                  </p>
                  <p className="mt-0.5 text-[11px] text-muted-foreground">
                    {run.MicrophoneRecordingPermanent
                      ? "Permanent"
                      : formatRemaining(run.MicrophoneRecordingExpiresAtUtc)}
                  </p>
                </div>
                <DropdownMenuSeparator />
                {!run.MicrophoneRecordingPermanent ? (
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
                    Keep permanently
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
                  Delete recording
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
              Microphone
            </DropdownMenuItem>
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem disabled>
            <span aria-hidden="true" className="size-4" />
            <HugeiconsIcon aria-hidden="true" icon={Download04Icon} className="size-4" />
            Export run
          </DropdownMenuItem>
          <DropdownMenuItem disabled>
            <span aria-hidden="true" className="size-4" />
            <HugeiconsIcon aria-hidden="true" icon={Upload04Icon} className="size-4" />
            Submit run
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
            Delete run
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <ConfirmDeleteDialog
        open={recordingDialogOpen}
        title="Delete microphone recording?"
        description={`This permanently deletes the ${formatFileSize(run.MicrophoneRecordingBytes)} recording for run #${run.RunIndex}. The run and replay will remain.`}
        actionLabel="Delete recording"
        pending={pendingAction === "delete-recording"}
        error={dialogError}
        onOpenChange={setRecordingDialogOpen}
        onConfirm={() => void deleteRecording()}
      />
      <ConfirmDeleteDialog
        open={runDialogOpen}
        title="Delete run?"
        description={`This permanently deletes run #${run.RunIndex}, its replay data, and its microphone recording. This cannot be undone.`}
        actionLabel="Delete run"
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
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            disabled={pending}
            onClick={onConfirm}
          >
            {pending ? "Deleting…" : actionLabel}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function formatRemaining(value: string | null) {
  if (!value) return "Deletes soon";
  const expiresAt = Date.parse(value);
  if (Number.isNaN(expiresAt)) return "Deletes soon";
  const remaining = expiresAt - Date.now();
  if (remaining <= 0) return "Deletes soon";
  const hours = Math.ceil(remaining / 3_600_000);
  if (hours < 24) return `Deletes in ${hours}h`;
  return `Deletes in ${Math.ceil(hours / 24)}d`;
}
