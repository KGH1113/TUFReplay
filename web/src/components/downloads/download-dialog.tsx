import { useTranslation } from "react-i18next";
import { installationBusy } from "@/api/downloads/downloads-api";
import type { DownloadCenterControl } from "@/hooks/downloads/use-download-center";
import { ApiError } from "@/shared/errors/api-error";
import { Button } from "@/shared/ui/button";
import { DetailsToggle } from "@/shared/ui/details-toggle";
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
        className="flex max-h-[92dvh] flex-col gap-0 overflow-hidden p-0"
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
        <DialogHeader className="shrink-0 gap-1 px-6 pt-6 pb-5">
          <DialogTitle>
            {renderer
              ? "OrbitRender"
              : t(
                  state?.Status === "ready"
                    ? "ffmpeg.readyTitle"
                    : state?.Status === "failed"
                      ? "ffmpeg.failedTitle"
                      : state && installationBusy(state) && state.Status !== "checking"
                        ? "ffmpeg.installingTitle"
                        : "ffmpeg.title",
                )}
          </DialogTitle>
          <DialogDescription>
            {t(renderer && reason === "render" ? "setup.forRender" : `description.${item}`)}
          </DialogDescription>
        </DialogHeader>
        {center.error ? (
          <div role="alert" className="mx-6 mb-5 space-y-2 text-sm text-destructive">
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
              <DetailsToggle label={t("details")}>
                <p className="break-words">{center.error.message}</p>
              </DetailsToggle>
            ) : null}
            <Button size="sm" variant="outline" onClick={center.refresh}>
              {t("retry")}
            </Button>
          </div>
        ) : null}
        {state && renderer ? (
          <div className="min-h-0 overflow-y-auto px-6 pb-6">
            <OrbitRenderSetup
              state={state}
              pending={center.pending}
              onAction={(action) => center.act("renderer", action)}
              onClose={center.close}
            />
          </div>
        ) : state ? (
          <DownloadItem
            item={item}
            state={state}
            pending={center.pending}
            onAction={center.act}
            onClose={center.close}
          />
        ) : (
          <div className="space-y-5 px-6 pb-6">
            <p role="status" className="text-sm text-muted-foreground">
              {t("loading")}
            </p>
            <Button variant="outline" onClick={center.close}>
              {t("close")}
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
