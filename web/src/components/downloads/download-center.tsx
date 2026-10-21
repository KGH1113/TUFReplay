import { Download04Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { Popover as PopoverPrimitive } from "radix-ui";
import { useTranslation } from "react-i18next";
import { installationBusy } from "@/api/downloads/downloads-api";
import { useDownloadCenter } from "@/hooks/downloads/use-download-center";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { Button } from "@/shared/ui/button";
import { Popover, PopoverContent } from "@/shared/ui/popover";
import { DownloadDialog } from "./download-dialog";

export function DownloadCenter({ connectionStatus }: { connectionStatus: ConnectionStatus }) {
  const { t } = useTranslation("downloads");
  const center = useDownloadCenter(connectionStatus === "online");
  const ffmpeg = center.state?.Ffmpeg;
  const busy = ffmpeg ? installationBusy(ffmpeg) : false;
  const installed = ffmpeg?.Status === "ready" || ffmpeg?.Status === "restart-required";
  return (
    <>
      <Popover open={center.menuOpen} onOpenChange={center.setMenuOpen}>
        <PopoverPrimitive.Trigger asChild>
          <Button
            ref={center.trigger}
            variant="ghost"
            size="icon-sm"
            aria-label={t("title")}
            title={t("title")}
            disabled={connectionStatus !== "online"}
          >
            <HugeiconsIcon icon={Download04Icon} size={17} strokeWidth={2} aria-hidden="true" />
          </Button>
        </PopoverPrimitive.Trigger>
        <PopoverContent
          className="w-[min(21rem,calc(100vw-2rem))] rounded-xl p-0"
          aria-label={t("title")}
        >
          <h2 className="px-4 pb-2 pt-4 text-xs font-medium text-muted-foreground">{t("title")}</h2>
          {center.loading && !center.error ? (
            <p role="status" className="px-4 py-3 text-xs text-muted-foreground">
              {t("loading")}
            </p>
          ) : null}
          {center.error ? (
            <Button
              variant="ghost"
              className="w-full justify-between px-4 text-sm"
              onClick={() => center.showDetails("ffmpeg")}
            >
              {t("connectionIssue")}
              <span className="text-xs text-muted-foreground">{t("view")}</span>
            </Button>
          ) : null}
          <div className="divide-y divide-border/50 px-4 pb-1">
            <div className="flex min-h-18 items-center justify-between gap-4 py-3">
              <div className="min-w-0 space-y-1">
                <p className="text-sm font-medium">TUFReplay-Renderer</p>
                <p className="text-xs leading-relaxed text-muted-foreground">
                  {t("rendererComingSoon")}
                </p>
              </div>
              <Button size="sm" variant="outline" disabled>
                {t("comingSoon")}
              </Button>
            </div>
            {ffmpeg ? (
              <div className="flex min-h-18 items-center justify-between gap-4 py-3">
                <div className="min-w-0 space-y-1">
                  <p className="text-sm font-medium">FFmpeg</p>
                  <p className="text-xs text-muted-foreground">
                    {t(`status.${ffmpeg.Status}`)}
                    {ffmpeg.Version ? ` · ${ffmpeg.Version}` : ""}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant={installed || busy ? "ghost" : "outline"}
                  disabled={ffmpeg.Status === "checking" || center.pending}
                  onClick={() =>
                    center.showDetails("ffmpeg", !installed && !busy && ffmpeg.Status !== "failed")
                  }
                >
                  {installed || busy || ffmpeg.Status === "failed"
                    ? t("view")
                    : ffmpeg.Status === "awaiting-consent"
                      ? t("continue")
                      : t("install")}
                </Button>
              </div>
            ) : null}
          </div>
        </PopoverContent>
      </Popover>
      <DownloadDialog center={center} />
    </>
  );
}
