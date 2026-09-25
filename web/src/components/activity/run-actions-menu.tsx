import {
  ArrowLeft01Icon,
  ArrowRight01Icon,
  ArrowUpRight01Icon,
  Delete02Icon,
  Download01Icon,
  FloppyDiskIcon,
  Loading03Icon,
  Mic01Icon,
  MoreVerticalIcon,
  Upload04Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { TFunction } from "i18next";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { SubmissionGalleryDialog } from "@/components/submission/submission-gallery-dialog";
import { SubmissionProgressDialog } from "@/components/submission/submission-progress-dialog";
import { useSubmissionRun } from "@/hooks/submission/use-submission";
import type { ActivityRun } from "@/models/activity/activity-model";
import { formatFileSize } from "@/models/activity/file-size";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import type { VisualSelection } from "@/models/submission/submission-model";
import {
  canSubmit,
  hasSubmissionPermission,
  submissionAccountKey,
  submissionPresentationForRequest,
} from "@/models/submission/submission-model";
import { submissionProgress } from "@/models/submission/submission-progress";
import { TUF_WEB_URL } from "@/shared/config/tuf-web-url";
import { TUFREPLAY_WEB_BUILD } from "@/shared/config/tufreplay-build-info";
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

type PendingAction = "download" | "keep" | "delete-recording" | "submit" | "delete-run" | null;

export function RunActionsMenu({
  run,
  disabled,
  runDeleteDisabled,
  onKeepMicrophoneRecording,
  onDownloadMicrophoneRecording,
  onDeleteMicrophoneRecording,
  onDeleteRun,
}: {
  run: ActivityRun;
  disabled: boolean;
  runDeleteDisabled: boolean;
  onKeepMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onDownloadMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onDeleteMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onDeleteRun: (run: ActivityRun) => Promise<void>;
}) {
  const { t } = useTranslation("replay");
  const { t: activityT } = useTranslation("activity");
  const { t: microphoneT, i18n } = useTranslation("microphone");
  const { t: submissionT } = useTranslation("submission");
  const locale = i18n.resolvedLanguage ?? "en";
  const [menuOpen, setMenuOpen] = useState(false);
  const [recordingDialogOpen, setRecordingDialogOpen] = useState(false);
  const [runDialogOpen, setRunDialogOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const [menuError, setMenuError] = useState("");
  const [dialogError, setDialogError] = useState("");
  const [galleryOpen, setGalleryOpen] = useState(false);
  const [progressOpen, setProgressOpen] = useState(false);
  const menuTriggerRef = useRef<HTMLButtonElement>(null);
  const [attemptedPresentation, setAttemptedPresentation] = useState<VisualSelection | null>(null);
  const busy = pendingAction !== null;
  const submission = useSubmissionRun(
    run.submissionRunId,
    (menuOpen || galleryOpen || progressOpen) && !disabled && run.submissionRunId !== null,
  );
  const progress = submissionProgress(submission.run.data);
  const submissionReady =
    !disabled &&
    !submission.run.isError &&
    progress.phase !== "submitted" &&
    hasSubmissionPermission(submission.status.data, submission.status.isError) &&
    submission.run.data !== undefined &&
    canSubmit(submission.run.data);

  const downloadRecording = async () => {
    if (disabled || busy) return;
    setPendingAction("download");
    setMenuError("");
    try {
      await onDownloadMicrophoneRecording(run);
      setMenuOpen(false);
    } catch (cause) {
      setMenuError(localizedErrorMessage(cause, microphoneT("recording.downloadFailed")));
    } finally {
      setPendingAction(null);
    }
  };

  useEffect(() => {
    if (run.submissionRunId !== null) setAttemptedPresentation(null);
  }, [run.submissionRunId]);

  const keepRecording = async () => {
    if (disabled || busy) return;
    setPendingAction("keep");
    setMenuError("");
    try {
      await onKeepMicrophoneRecording(run);
    } catch (cause) {
      setMenuError(localizedErrorMessage(cause, microphoneT("recording.keepFailed")));
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
      setDialogError(localizedErrorMessage(cause, microphoneT("recording.deleteFailed")));
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
      setDialogError(localizedErrorMessage(cause, t("run.deleteFailed")));
    } finally {
      setPendingAction(null);
    }
  };

  const submitRun = async () => {
    if (disabled || busy || !submissionReady) return;
    if (submission.run.data?.presentation === null) {
      setMenuError("");
      setMenuOpen(false);
      setProgressOpen(false);
      setGalleryOpen(true);
      return;
    }
    setPendingAction("submit");
    setMenuError("");
    try {
      await submission.submit.mutateAsync(undefined);
    } catch {
      setMenuError(submissionT("requestFailed"));
    } finally {
      setPendingAction(null);
    }
  };

  const submitFromGallery = async (presentation: VisualSelection | undefined) => {
    if (disabled || busy || !submissionReady) return;
    const nextPresentation = submissionPresentationForRequest(
      submission.run.data?.presentation,
      presentation,
      attemptedPresentation,
    );
    if (submission.run.data?.presentation == null && nextPresentation === undefined) return;
    if (nextPresentation) setAttemptedPresentation(nextPresentation);
    setPendingAction("submit");
    setMenuError("");
    try {
      await submission.submit.mutateAsync(nextPresentation);
    } catch {
      setMenuError(submissionT("requestFailed"));
    } finally {
      setPendingAction(null);
      setGalleryOpen(false);
      setProgressOpen(true);
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
            ref={menuTriggerRef}
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={activityT("run.openActions", { runIndex: run.runIndex })}
          >
            <HugeiconsIcon aria-hidden="true" icon={MoreVerticalIcon} className="size-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align="end"
          className="w-fit min-w-0 whitespace-nowrap"
          onCloseAutoFocus={(event) => {
            if (progressOpen || galleryOpen) event.preventDefault();
          }}
        >
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
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {run.microphoneRecordingPermanent
                      ? microphoneT("recording.permanent")
                      : formatRemaining(run.microphoneRecordingExpiresAtUtc, microphoneT)}
                  </p>
                </div>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  disabled={disabled || busy}
                  onSelect={(event) => {
                    event.preventDefault();
                    void downloadRecording();
                  }}
                >
                  <HugeiconsIcon
                    aria-hidden="true"
                    icon={pendingAction === "download" ? Loading03Icon : Download01Icon}
                    className={pendingAction === "download" ? "size-4 animate-spin" : "size-4"}
                  />
                  {microphoneT("recording.download")}
                </DropdownMenuItem>
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
          {TUFREPLAY_WEB_BUILD.flavor === "auto-submission" ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                disabled={
                  disabled || (busy && pendingAction !== "submit") || run.submissionRunId === null
                }
                onSelect={() => {
                  setMenuError("");
                  setProgressOpen(true);
                }}
              >
                <span aria-hidden="true" className="size-4" />
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={
                    progress.processing || pendingAction === "submit" ? Loading03Icon : Upload04Icon
                  }
                  className={
                    progress.processing || pendingAction === "submit"
                      ? "size-4 animate-spin motion-reduce:animate-none"
                      : "size-4"
                  }
                />
                {submission.run.data
                  ? submissionT(`progress.heading.${progress.phase}`)
                  : submissionT("progress.title")}
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={ArrowRight01Icon}
                  className="ml-auto size-4"
                />
              </DropdownMenuItem>
              {submission.run.data?.external_pass_id != null ? (
                <DropdownMenuItem asChild>
                  <a
                    href={`${TUF_WEB_URL}/passes/${submission.run.data.external_pass_id}`}
                    target="_blank"
                    rel="noreferrer"
                  >
                    <span aria-hidden="true" className="size-4" />
                    <HugeiconsIcon
                      aria-hidden="true"
                      icon={ArrowUpRight01Icon}
                      className="size-4"
                    />
                    {submissionT("viewPass")}
                  </a>
                </DropdownMenuItem>
              ) : null}
            </>
          ) : null}
          {menuError ? (
            <p
              aria-live="polite"
              className="max-w-56 whitespace-normal px-2 py-1 text-xs text-destructive"
            >
              {menuError}
            </p>
          ) : null}
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

      <SubmissionProgressDialog
        open={progressOpen}
        run={submission.run.data}
        runIndex={run.runIndex}
        ready={submissionReady}
        pending={pendingAction === "submit"}
        queryFailed={submission.run.isError || submission.status.isError}
        disconnected={submission.status.data?.connected === false}
        refreshing={submission.run.isFetching || submission.status.isFetching}
        error={menuError}
        onOpenChange={setProgressOpen}
        onSubmit={() => void submitRun()}
        onRefresh={() => {
          void submission.status.refetch();
          void submission.run.refetch();
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          if (!galleryOpen) menuTriggerRef.current?.focus();
        }}
      />
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
      <SubmissionGalleryDialog
        open={galleryOpen}
        run={submission.run.data}
        locked={submission.run.data?.presentation != null}
        initialSelection={attemptedPresentation}
        accountKey={submissionAccountKey(submission.status.data)}
        pending={pendingAction === "submit"}
        error={menuError}
        onOpenChange={setGalleryOpen}
        onSubmit={(presentation) => void submitFromGallery(presentation)}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          if (!progressOpen) menuTriggerRef.current?.focus();
        }}
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
