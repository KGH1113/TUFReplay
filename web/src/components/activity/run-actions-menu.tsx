import {
  Delete02Icon,
  FolderLibraryIcon,
  MoreVerticalIcon,
  Upload04Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { RunRecordingsDialog } from "@/components/activity/run-recordings-dialog";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import type { RunRecordingAction } from "@/models/activity/run-recordings";
import { Button } from "@/shared/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/shared/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/shared/ui/dropdown-menu";

export function RunActionsMenu({
  run,
  disabled,
  runDeleteDisabled,
  onRender,
  renderDisabled = false,
  onKeepMicrophoneRecording,
  onDownloadMicrophoneRecording,
  onDeleteMicrophoneRecording,
  onKeepCameraRecording,
  onDownloadCameraRecording,
  onDeleteCameraRecording,
  onDeleteRun,
}: {
  run: ActivityRun;
  disabled: boolean;
  runDeleteDisabled: boolean;
  onRender?: (run: ActivityRun, trigger?: HTMLElement) => void;
  renderDisabled?: boolean;
  onKeepMicrophoneRecording: RunRecordingAction;
  onDownloadMicrophoneRecording: RunRecordingAction;
  onDeleteMicrophoneRecording: RunRecordingAction;
  onKeepCameraRecording: RunRecordingAction;
  onDownloadCameraRecording: RunRecordingAction;
  onDeleteCameraRecording: RunRecordingAction;
  onDeleteRun: RunRecordingAction;
}) {
  const { t } = useTranslation("replay");
  const { t: activityT } = useTranslation("activity");
  const { t: renderT } = useTranslation("render");
  const { t: commonT } = useTranslation("common");
  const [menuOpen, setMenuOpen] = useState(false);
  const [recordingsOpen, setRecordingsOpen] = useState(false);
  const [runDialogOpen, setRunDialogOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");
  const trigger = useRef<HTMLButtonElement>(null);
  const openingDialog = useRef(false);

  const deleteRun = async () => {
    if (disabled || runDeleteDisabled || deleting) return;
    setDeleting(true);
    setError("");
    try {
      await onDeleteRun(run);
      setRunDialogOpen(false);
    } catch (cause) {
      setError(localizedErrorMessage(cause, t("run.deleteFailed")));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <>
      <DropdownMenu open={menuOpen} onOpenChange={setMenuOpen}>
        <DropdownMenuTrigger asChild>
          <Button
            ref={trigger}
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
            if (openingDialog.current) {
              event.preventDefault();
              openingDialog.current = false;
            }
          }}
        >
          {onRender ? (
            <>
              <DropdownMenuItem
                disabled={disabled || deleting || renderDisabled}
                onSelect={() => {
                  openingDialog.current = true;
                  setMenuOpen(false);
                  onRender(run, trigger.current ?? undefined);
                }}
              >
                <RenderSlateIcon />
                {renderT("button")}
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          ) : null}
          <DropdownMenuItem
            disabled={!run.hasMicrophoneRecording && !run.hasCameraRecording}
            onSelect={() => {
              openingDialog.current = true;
              setMenuOpen(false);
              setRecordingsOpen(true);
            }}
          >
            <HugeiconsIcon aria-hidden="true" icon={FolderLibraryIcon} className="size-4" />
            {activityT("recordings.label")}
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem disabled>
            <HugeiconsIcon aria-hidden="true" icon={Upload04Icon} className="size-4" />
            {t("run.submit")}
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            className="text-destructive data-highlighted:bg-destructive/10 data-highlighted:text-destructive"
            disabled={disabled || runDeleteDisabled || deleting}
            onSelect={() => {
              openingDialog.current = true;
              setMenuOpen(false);
              setError("");
              setRunDialogOpen(true);
            }}
          >
            <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
            {t("run.delete")}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      <RunRecordingsDialog
        run={run}
        open={recordingsOpen}
        disabled={disabled}
        deletionDisabled={runDeleteDisabled}
        onOpenChange={setRecordingsOpen}
        restoreFocus={() => trigger.current?.focus()}
        actions={{
          microphone: {
            download: onDownloadMicrophoneRecording,
            keep: onKeepMicrophoneRecording,
            delete: onDeleteMicrophoneRecording,
          },
          camera: {
            download: onDownloadCameraRecording,
            keep: onKeepCameraRecording,
            delete: onDeleteCameraRecording,
          },
        }}
      />
      <Dialog open={runDialogOpen} onOpenChange={(open) => !deleting && setRunDialogOpen(open)}>
        <DialogContent
          className="min-h-0 w-[min(25rem,calc(100vw-2rem))]"
          onEscapeKeyDown={(event) => deleting && event.preventDefault()}
          onPointerDownOutside={(event) => deleting && event.preventDefault()}
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            trigger.current?.focus();
          }}
        >
          <DialogTitle>{t("run.deleteTitle")}</DialogTitle>
          <DialogDescription className="mt-2 leading-relaxed">
            {t("run.deleteDescription", { runIndex: run.runIndex })}
          </DialogDescription>
          {error ? (
            <p role="alert" className="mt-3 text-sm text-destructive">
              {error}
            </p>
          ) : null}
          <div className="mt-6 flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={deleting}
              onClick={() => setRunDialogOpen(false)}
            >
              {commonT("actions.cancel")}
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              disabled={deleting}
              onClick={() => void deleteRun()}
            >
              {deleting ? t("run.deleting") : t("run.delete")}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}

function RenderSlateIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="m3 10-.7-3.8 17.6-3.3.7 3.9L3 10Z" />
      <path d="m6.7 5.4 2.7 3.4m3.5-4.6 2.7 3.5M3 10h18v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-9Z" />
    </svg>
  );
}
