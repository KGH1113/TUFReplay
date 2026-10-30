import { Loading03Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import type { useRunDeleteRequest } from "@/hooks/activity/use-run-delete-request";
import { formatFileSize } from "@/models/activity/file-size";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/shared/ui/alert-dialog";
import { Button } from "@/shared/ui/button";

export function RunDeleteDialog({ request }: { request: ReturnType<typeof useRunDeleteRequest> }) {
  const { t, i18n } = useTranslation("replay");
  const { t: activityT } = useTranslation("activity");
  const { t: commonT } = useTranslation("common");
  const run = request.run;
  const locale = i18n.resolvedLanguage ?? "en";
  const mediaBytes =
    (run?.hasMicrophoneRecording ? run.microphoneRecordingBytes : 0) +
    (run?.hasCameraRecording ? run.cameraRecordingBytes : 0);

  return (
    <AlertDialog open={request.open} onOpenChange={request.setOpen}>
      <AlertDialogContent
        onEscapeKeyDown={(event) => request.deleting && event.preventDefault()}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          request.restoreFocus();
        }}
      >
        <AlertDialogHeader>
          <AlertDialogTitle>
            {activityT("runDelete.title", { runIndex: run?.runIndex ?? "" })}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {t("run.deleteDescription", { runIndex: run?.runIndex ?? "" })}
            {mediaBytes > 0
              ? ` ${activityT("runDelete.media", { size: formatFileSize(mediaBytes, locale) })}`
              : ""}
          </AlertDialogDescription>
        </AlertDialogHeader>
        {request.error ? (
          <p role="alert" className="text-sm text-destructive">
            {request.error}
          </p>
        ) : null}
        <AlertDialogFooter>
          <Button
            type="button"
            variant="ghost"
            disabled={request.deleting}
            onClick={() => request.setOpen(false)}
          >
            {commonT("actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={request.deleting}
            onClick={() => void request.confirm()}
          >
            {request.deleting ? (
              <HugeiconsIcon
                aria-hidden="true"
                icon={Loading03Icon}
                className="size-4 animate-spin motion-reduce:animate-none"
              />
            ) : null}
            {request.deleting ? t("run.deleting") : t("run.delete")}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
