import { useTranslation } from "react-i18next";
import type { DownloadCenterControl } from "@/hooks/downloads/use-download-center";
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
import { OrbitRenderSetup } from "./orbit-render-setup";

export function DownloadDialog({ center }: { center: DownloadCenterControl }) {
  const { t } = useTranslation("downloads");
  const { item, reason } = center.dialog;
  const renderer = item === "renderer";
  const state = renderer ? center.state?.Renderer : center.state?.Ffmpeg;
  const needsUpdate =
    center.error instanceof ApiError &&
    (center.error.code === "downloads_unavailable" || center.error.code === "handler_not_found");
  return (
    <Dialog
      open={center.dialogOpen}
      onOpenChange={(open) => {
        if (!open) center.close();
      }}
    >
      <DialogContent
        className="max-h-[calc(100dvh-2rem)] space-y-5 overflow-y-auto rounded-2xl p-6"
        // Focus the dialog itself, so no button shows a ring or takes Enter before it is read.
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          if (event.currentTarget instanceof HTMLElement) event.currentTarget.focus();
        }}
        onInteractOutside={(event) => event.preventDefault()}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          center.restoreFocus();
        }}
      >
        <DialogHeader>
          <DialogTitle>{renderer ? "OrbitRender" : "FFmpeg"}</DialogTitle>
          <DialogDescription>
            {t(renderer && reason === "render" ? "setup.forRender" : `description.${item}`)}
          </DialogDescription>
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
        {state && renderer ? (
          <OrbitRenderSetup
            state={state}
            pending={center.pending}
            onAction={(action) => center.act("renderer", action)}
            onClose={center.close}
          />
        ) : state ? (
          <DownloadItem
            item={item}
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
