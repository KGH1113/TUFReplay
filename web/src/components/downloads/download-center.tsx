import { Download04Icon } from "@hugeicons/core-free-icons";
import type { TFunction } from "i18next";
import { Popover as PopoverPrimitive } from "radix-ui";
import { useTranslation } from "react-i18next";
import { type InstallState, installationBusy } from "@/api/downloads/downloads-api";
import { SidebarSettingButton } from "@/components/shell/sidebar-setting-button";
import { useDownloadCenter } from "@/hooks/downloads/use-download-center";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { Button } from "@/shared/ui/button";
import { Popover, PopoverContent } from "@/shared/ui/popover";
import { DownloadDialog } from "./download-dialog";

export function DownloadCenter({ connectionStatus }: { connectionStatus: ConnectionStatus }) {
  const { t } = useTranslation("downloads");
  const { t: commonT } = useTranslation("common");
  const center = useDownloadCenter(connectionStatus === "online");
  const renderer = center.state?.Renderer;
  const ffmpeg = center.state?.Ffmpeg;
  const busy = ffmpeg ? installationBusy(ffmpeg) : false;
  const installed = ffmpeg?.Status === "ready" || ffmpeg?.Status === "restart-required";
  return (
    <>
      <Popover open={center.menuOpen} onOpenChange={center.setMenuOpen}>
        <PopoverPrimitive.Trigger asChild>
          <SidebarSettingButton
            ref={center.trigger}
            icon={Download04Icon}
            label={t("title")}
            value={
              connectionStatus !== "online"
                ? commonT("settings.needsConnection")
                : center.error
                  ? t("connectionIssue")
                  : ffmpeg
                    ? downloadsSummary(ffmpeg, busy, t)
                    : undefined
            }
            attention={
              connectionStatus === "online" &&
              (Boolean(center.error) ||
                ffmpeg?.Status === "failed" ||
                ffmpeg?.Status === "awaiting-consent" ||
                ffmpeg?.Status === "restart-required" ||
                renderer?.Status === "failed" ||
                renderer?.Status === "awaiting-consent" ||
                renderer?.Status === "restart-required")
            }
            aria-label={t("title")}
            disabled={connectionStatus !== "online"}
          />
        </PopoverPrimitive.Trigger>
        <PopoverContent
          side="right"
          align="end"
          sideOffset={12}
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
            {renderer ? (
              <div className="flex min-h-18 items-center justify-between gap-4 py-3">
                <div className="min-w-0 space-y-1">
                  <p className="text-sm font-medium">OrbitRender</p>
                  <p className="text-xs text-muted-foreground">
                    {t(`status.${renderer.Status}`)}
                    {renderer.Version ? ` · ${renderer.Version}` : ""}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant={renderer.Status === "missing" ? "outline" : "ghost"}
                  disabled={renderer.Status === "checking" || center.pending}
                  onClick={() =>
                    center.showDetails(
                      "renderer",
                      ["missing", "cancelled", "declined"].includes(renderer.Status),
                    )
                  }
                >
                  {renderer.Status === "missing" ||
                  renderer.Status === "cancelled" ||
                  renderer.Status === "declined"
                    ? t("install")
                    : renderer.Status === "awaiting-consent"
                      ? t("continue")
                      : t("view")}
                </Button>
              </div>
            ) : null}
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

/** One line for the sidebar that never names a single component, since the center lists several. */
function downloadsSummary(
  state: InstallState,
  busy: boolean,
  t: TFunction<"downloads">,
): string | undefined {
  if (busy && state.Status !== "checking")
    return state.TotalBytes
      ? t("summary.downloadingPercent", {
          percent: Math.round((state.DownloadedBytes / state.TotalBytes) * 100),
        })
      : t("summary.downloading");
  if (state.Status === "awaiting-consent") return t("summary.approval");
  if (state.Status === "failed") return t("summary.failed");
  if (state.Status === "restart-required") return t("summary.restart");
  return undefined;
}
