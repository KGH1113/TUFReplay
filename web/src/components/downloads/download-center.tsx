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
  const installedState = center.state;
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
          {installedState ? (
            <div className="divide-y divide-border/50 px-4 pb-1">
              {(["renderer", "ffmpeg"] as const).map((item) => {
                const state = item === "renderer" ? installedState.Renderer : installedState.Ffmpeg;
                const busy = installationBusy(state);
                const installed = state.Status === "ready" || state.Status === "restart-required";
                return (
                  <div key={item} className="flex min-h-18 items-center justify-between gap-4 py-3">
                    <div className="min-w-0 space-y-1">
                      <p className="text-sm font-medium">
                        {item === "renderer" ? "TUFReplay-Renderer" : "FFmpeg"}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {t(`status.${state.Status}`)}
                        {state.Version ? ` · ${state.Version}` : ""}
                      </p>
                    </div>
                    <Button
                      size="sm"
                      variant={installed || busy ? "ghost" : "outline"}
                      disabled={state.Status === "checking" || center.pending}
                      onClick={() =>
                        center.showDetails(item, !installed && !busy && state.Status !== "failed")
                      }
                    >
                      {installed || busy || state.Status === "failed"
                        ? t("view")
                        : state.Status === "awaiting-consent"
                          ? t("continue")
                          : t("install")}
                    </Button>
                  </div>
                );
              })}
            </div>
          ) : null}
        </PopoverContent>
      </Popover>
      <DownloadDialog center={center} />
    </>
  );
}
