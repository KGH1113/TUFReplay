import { useTranslation } from "react-i18next";
import type { useDownloadCenter } from "@/hooks/downloads/use-download-center";
import { ApiError } from "@/shared/errors/api-error";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { DownloadItem } from "./download-item";

export function DownloadDialog({ center }: { center: ReturnType<typeof useDownloadCenter> }) {
  const { t } = useTranslation("downloads");
  const state = center.item === "renderer" ? center.state?.Renderer : center.state?.Ffmpeg;
  const name = center.item === "renderer" ? "TUFReplay-Renderer" : "FFmpeg";
  const needsUpdate =
    center.error instanceof ApiError &&
    (center.error.code === "downloads_unavailable" || center.error.code === "handler_not_found");
  return (
    <Dialog
      open={Boolean(center.item)}
      onOpenChange={(open) => {
        if (!open) center.close();
      }}
    >
      <DialogContent
        className="max-h-[calc(100dvh-2rem)] space-y-5 overflow-y-auto rounded-2xl p-6"
        onInteractOutside={(event) => event.preventDefault()}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          center.restoreFocus();
        }}
      >
        <DialogHeader>
          <DialogTitle>{name}</DialogTitle>
          <DialogDescription>{t(`description.${center.item ?? "ffmpeg"}`)}</DialogDescription>
        </DialogHeader>
        {center.error ? (
          <div role="alert" className="space-y-2 text-sm text-destructive">
            <p>
              {t(
                needsUpdate
                  ? "errors.downloads_unavailable"
                  : center.actionFailed
                    ? "errors.actionFailed"
                    : "errors.statusFailed",
              )}
            </p>
            {center.error instanceof Error ? (
              <details className="text-xs">
                <summary>{t("details")}</summary>
                <p className="break-words">{center.error.message}</p>
              </details>
            ) : null}
            <Button size="sm" variant="outline" onClick={center.refresh}>
              {t("retry")}
            </Button>
          </div>
        ) : null}
        {center.item && state ? (
          <DownloadItem
            item={center.item}
            state={state}
            pending={center.pending}
            onAction={center.act}
            onClose={center.close}
          />
        ) : (
          <>
            <p role="status" className="text-sm text-muted-foreground">
              {t("loading")}
            </p>
            <Button variant="outline" onClick={center.close}>
              {t("close")}
            </Button>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
