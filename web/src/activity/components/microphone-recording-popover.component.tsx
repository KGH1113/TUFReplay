import {
  Clock01Icon,
  Delete02Icon,
  FloppyDiskIcon,
  Loading03Icon,
  Mic01Icon,
  PinIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { type MouseEvent, useEffect, useId, useRef, useState } from "react";

import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle } from "@/ui/dialog.component";
import { Popover, PopoverAnchor, PopoverContent } from "@/ui/popover.component";
import { cn } from "@/ui/ui-class.utils";
import type { ActivityRun } from "../activity.model";
import { formatFileSize } from "../lib/file-size.format";

const CLOSE_DELAY_MS = 160;

export function MicrophoneRecordingPopover({
  run,
  pinnedRunId,
  onPinnedRunChange,
  onKeep,
  onDelete,
}: {
  run: ActivityRun;
  pinnedRunId: string | null;
  onPinnedRunChange: (runId: string | null) => void;
  onKeep: (run: ActivityRun) => Promise<void>;
  onDelete: (run: ActivityRun) => Promise<void>;
}) {
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const contentId = useId();
  const [hoverOpen, setHoverOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<"keep" | "delete" | null>(null);
  const [error, setError] = useState("");
  const [now, setNow] = useState(() => Date.now());
  const pinned = pinnedRunId === run.Id;
  const anotherPinned = pinnedRunId !== null && !pinned;
  const open =
    !deleteDialogOpen &&
    (pinned || (!anotherPinned && hoverOpen) || pendingAction === "keep" || Boolean(error));

  useEffect(
    () => () => {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    },
    [],
  );

  useEffect(() => {
    if (!open || run.MicrophoneRecordingPermanent) return;
    setNow(Date.now());
    const interval = setInterval(() => setNow(Date.now()), 60_000);
    return () => clearInterval(interval);
  }, [open, run.MicrophoneRecordingPermanent]);

  const keepOpen = () => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    closeTimerRef.current = null;
    if (!anotherPinned) setHoverOpen(true);
  };

  const scheduleClose = () => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    closeTimerRef.current = setTimeout(() => {
      closeTimerRef.current = null;
      setHoverOpen(false);
    }, CLOSE_DELAY_MS);
  };

  const dismiss = () => {
    if (pendingAction) return;
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    closeTimerRef.current = null;
    setHoverOpen(false);
    setError("");
    if (pinned) onPinnedRunChange(null);
  };

  const keepPermanently = async () => {
    if (pendingAction) return;
    onPinnedRunChange(run.Id);
    setPendingAction("keep");
    setError("");
    try {
      await onKeep(run);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not keep microphone recording");
    } finally {
      setPendingAction(null);
    }
  };

  const deleteRecording = async () => {
    if (pendingAction) return;
    onPinnedRunChange(run.Id);
    setPendingAction("delete");
    setError("");
    try {
      await onDelete(run);
      setDeleteDialogOpen(false);
      onPinnedRunChange(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not delete microphone recording");
    } finally {
      setPendingAction(null);
    }
  };

  const togglePinned = (event: MouseEvent<HTMLButtonElement>) => {
    setError("");
    if (pinned) {
      setHoverOpen(false);
      onPinnedRunChange(null);
      return;
    }
    setHoverOpen(true);
    onPinnedRunChange(run.Id);
    if (!pinned && event.detail === 0) {
      requestAnimationFrame(() =>
        contentRef.current?.querySelector<HTMLButtonElement>("button")?.focus(),
      );
    }
  };

  const openDeleteDialog = () => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    closeTimerRef.current = null;
    setHoverOpen(false);
    setError("");
    onPinnedRunChange(null);
    setDeleteDialogOpen(true);
  };

  return (
    <>
      <Popover open={open} onOpenChange={(nextOpen) => !nextOpen && dismiss()}>
        <PopoverAnchor asChild>
          <button
            ref={triggerRef}
            type="button"
            aria-label={`Microphone recording for run ${run.RunIndex}`}
            aria-haspopup="dialog"
            aria-expanded={open}
            aria-controls={contentId}
            onPointerEnter={keepOpen}
            onPointerLeave={scheduleClose}
            onFocus={keepOpen}
            onBlur={scheduleClose}
            onClick={togglePinned}
            className={cn(
              "grid size-8 place-items-center rounded-sm text-primary/75 transition-[color,transform] hover:scale-105 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
              open && "text-primary",
            )}
          >
            <HugeiconsIcon
              aria-hidden="true"
              icon={Mic01Icon}
              className="size-[18px]"
              strokeWidth={2}
            />
          </button>
        </PopoverAnchor>
        <PopoverContent
          ref={contentRef}
          id={contentId}
          role="dialog"
          aria-label={`Microphone recording controls for run ${run.RunIndex}`}
          onPointerEnter={keepOpen}
          onPointerLeave={scheduleClose}
          onInteractOutside={(event) => {
            if (triggerRef.current?.contains(event.target as Node)) event.preventDefault();
          }}
          onOpenAutoFocus={(event) => event.preventDefault()}
          onCloseAutoFocus={(event) => event.preventDefault()}
        >
          <div className="flex items-start gap-3">
            <div className="grid size-8 shrink-0 place-items-center rounded-md bg-primary/10 text-primary">
              <HugeiconsIcon
                aria-hidden="true"
                icon={Mic01Icon}
                className="size-4"
                strokeWidth={2}
              />
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-medium">Microphone recording</p>
                {pinned ? (
                  <span className="inline-flex items-center gap-1 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                    <HugeiconsIcon
                      aria-hidden="true"
                      icon={PinIcon}
                      className="size-3"
                      strokeWidth={2}
                    />
                    Pinned
                  </span>
                ) : null}
              </div>
              <p className="mt-0.5 text-xs tabular-nums text-muted-foreground">
                {formatFileSize(run.MicrophoneRecordingBytes)}
              </p>
            </div>
          </div>

          <div className="mt-3 flex items-end justify-between gap-3">
            <p className="flex min-w-0 items-center gap-1.5 text-xs text-muted-foreground">
              {run.MicrophoneRecordingPermanent ? (
                "Permanent"
              ) : (
                <>
                  <HugeiconsIcon
                    aria-hidden="true"
                    icon={Clock01Icon}
                    className="size-3.5 shrink-0"
                    strokeWidth={2}
                  />
                  {formatRemaining(run.MicrophoneRecordingExpiresAtUtc, now)}
                </>
              )}
            </p>

            <div className="flex shrink-0 items-center gap-1">
              {!run.MicrophoneRecordingPermanent ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  aria-label="Keep microphone recording permanently"
                  title="Keep permanently"
                  className="text-primary hover:text-primary"
                  disabled={pendingAction !== null}
                  onClick={() => void keepPermanently()}
                >
                  <HugeiconsIcon
                    aria-hidden="true"
                    icon={pendingAction === "keep" ? Loading03Icon : FloppyDiskIcon}
                    className={cn("size-4", pendingAction === "keep" && "animate-spin")}
                    strokeWidth={2}
                  />
                </Button>
              ) : null}
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label="Delete microphone recording"
                title="Delete recording"
                className="text-destructive hover:text-destructive"
                disabled={pendingAction !== null}
                onClick={openDeleteDialog}
              >
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={Delete02Icon}
                  className="size-4"
                  strokeWidth={2}
                />
              </Button>
            </div>
          </div>

          {error ? (
            <p aria-live="polite" className="mt-2 text-xs text-destructive">
              {error}
            </p>
          ) : null}
        </PopoverContent>
      </Popover>

      <Dialog
        open={deleteDialogOpen}
        onOpenChange={(nextOpen) => {
          if (pendingAction === "delete") return;
          setDeleteDialogOpen(nextOpen);
          if (!nextOpen) setError("");
        }}
      >
        <DialogContent
          className="min-h-0 w-[min(25rem,calc(100vw-2rem))]"
          aria-describedby={`microphone-delete-description-${run.Id}`}
          onEscapeKeyDown={(event) => pendingAction === "delete" && event.preventDefault()}
          onPointerDownOutside={(event) => pendingAction === "delete" && event.preventDefault()}
        >
          <DialogTitle>Delete microphone recording?</DialogTitle>
          <p
            id={`microphone-delete-description-${run.Id}`}
            className="mt-2 text-sm leading-relaxed text-muted-foreground"
          >
            This permanently deletes the {formatFileSize(run.MicrophoneRecordingBytes)} recording
            for run #{run.RunIndex}. The run and replay will remain.
          </p>
          {error ? (
            <p aria-live="polite" className="mt-3 text-sm text-destructive">
              {error}
            </p>
          ) : null}
          <div className="mt-6 flex justify-end gap-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              disabled={pendingAction === "delete"}
              onClick={() => {
                setDeleteDialogOpen(false);
                setError("");
              }}
            >
              Cancel
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              disabled={pendingAction === "delete"}
              onClick={() => void deleteRecording()}
            >
              {pendingAction === "delete" ? "Deleting…" : "Delete recording"}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}

function formatRemaining(value: string | null, now: number) {
  if (!value) return "Deletes soon";
  const expiresAt = Date.parse(value);
  if (Number.isNaN(expiresAt)) return "Deletes soon";
  const remainingMinutes = Math.max(0, Math.ceil((expiresAt - now) / 60_000));
  if (remainingMinutes <= 0) return "Deletes soon";
  const days = Math.floor(remainingMinutes / 1_440);
  const hours = Math.floor((remainingMinutes % 1_440) / 60);
  if (days > 0) return `Deletes in ${days}d ${hours}h`;
  if (hours > 0) return `Deletes in ${hours}h ${remainingMinutes % 60}m`;
  return `Deletes in ${remainingMinutes}m`;
}
