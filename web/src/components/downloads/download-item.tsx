import { Alert02Icon, Download04Icon, Loading03Icon, Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import {
  type DownloadAction,
  type DownloadItemId,
  type InstallState,
  installationBusy,
} from "@/api/downloads/downloads-api";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { DetailsToggle } from "@/shared/ui/details-toggle";
import { Progress } from "@/shared/ui/progress";
import { ScrollArea } from "@/shared/ui/scroll-area";

export function DownloadItem({
  item,
  state,
  pending,
  onAction,
  onClose,
}: {
  item: DownloadItemId;
  state: InstallState;
  pending: boolean;
  onAction: (item: DownloadItemId, action: DownloadAction) => void;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation("downloads");
  const busy = installationBusy(state);
  const checking = state.Status === "checking";
  const done = state.Status === "ready" || state.Status === "restart-required";
  const failed = state.Status === "failed";
  // Byte progress only describes the download, not extraction or verification.
  const progress =
    state.Status === "downloading" && state.TotalBytes
      ? Math.max(0, Math.min(100, (state.DownloadedBytes / state.TotalBytes) * 100))
      : null;
  const knownError =
    state.ErrorCode && i18n.exists(`errors.${state.ErrorCode}`, { ns: "downloads" });
  const icon = busy ? Loading03Icon : done ? Tick02Icon : failed ? Alert02Icon : Download04Icon;
  const install = () => {
    if (state.Status !== "awaiting-consent") onAction(item, "request");
    onAction(item, "confirm");
  };

  return (
    <>
      <ScrollArea className="min-h-0 [&_[data-radix-scroll-area-viewport]]:max-h-[calc(92dvh-12rem)] [&_[data-radix-scroll-area-viewport]>div]:block!">
        <div className="space-y-5 px-6 pb-6">
          {busy || done || failed ? (
            <div className="flex items-start gap-3">
              <div
                className={cn(
                  "grid size-10 shrink-0 place-items-center rounded-xl bg-muted",
                  failed ? "text-destructive" : "text-primary",
                )}
              >
                <HugeiconsIcon
                  icon={icon}
                  aria-hidden="true"
                  size={20}
                  className={cn(busy && "motion-safe:animate-spin")}
                />
              </div>
              <div className="min-w-0 space-y-1">
                <p
                  role={failed ? "alert" : "status"}
                  aria-live="polite"
                  className="text-sm font-medium leading-5"
                >
                  {busy || failed ? t(`status.${state.Status}`) : done ? t("ffmpeg.ready") : null}
                </p>
                <p className="text-sm leading-relaxed text-muted-foreground">
                  {failed
                    ? knownError
                      ? t(`errors.${state.ErrorCode}` as never)
                      : t("errors.installFailed")
                    : done
                      ? t(
                          state.Status === "restart-required"
                            ? "ffmpeg.restartHelp"
                            : "ffmpeg.readyHelp",
                        )
                      : busy
                        ? t(checking ? "loading" : "ffmpeg.backgroundHelp")
                        : null}
                </p>
                {failed && state.Error ? (
                  <DetailsToggle label={t("details")} className="pt-1">
                    <p className="whitespace-pre-wrap break-words text-xs leading-relaxed text-muted-foreground">
                      {state.Error}
                    </p>
                  </DetailsToggle>
                ) : null}
              </div>
            </div>
          ) : null}
          {busy && !checking ? (
            <div className="space-y-2.5">
              <div className="flex items-baseline justify-between gap-3 text-xs tabular-nums text-muted-foreground">
                <span>
                  {progress === null
                    ? t("ffmpeg.preparing")
                    : t("ffmpeg.percent", { percent: Math.floor(progress) })}
                </span>
                {state.Status === "downloading" && state.DownloadedBytes > 0 ? (
                  <span>
                    {(state.DownloadedBytes / 1048576).toFixed(1)}
                    {state.TotalBytes ? ` / ${(state.TotalBytes / 1048576).toFixed(1)}` : ""} MB
                  </span>
                ) : null}
              </div>
              <Progress
                aria-label={t("progress", { name: "FFmpeg" })}
                value={progress}
                className="h-2"
              />
            </div>
          ) : null}
          <DetailsToggle label={t("ffmpeg.installInfo")}>
            <div className="space-y-2 text-xs leading-relaxed text-muted-foreground">
              <p>{t("consent.ffmpeg")}</p>
              {state.InstallationDirectory ? (
                <p className="break-all">
                  {t("installLocation", { path: state.InstallationDirectory })}
                </p>
              ) : null}
              {state.Source ? (
                <p className="break-all">{t("ffmpeg.source", { source: state.Source })}</p>
              ) : null}
              {state.Version ? <p className="break-words">{state.Version}</p> : null}
              <a
                href="https://ffmpeg.org/legal.html"
                target="_blank"
                rel="noreferrer"
                className="text-primary underline underline-offset-2"
              >
                {t("license")}
              </a>
            </div>
          </DetailsToggle>
        </div>
      </ScrollArea>
      <div className="flex shrink-0 flex-wrap justify-end gap-2 border-t border-border bg-muted/20 px-6 py-4">
        {busy && !checking ? (
          <Button variant="ghost" disabled={pending} onClick={() => onAction(item, "cancel")}>
            {t("cancel")}
          </Button>
        ) : null}
        {done ? (
          <Button className="min-w-24" onClick={onClose}>
            {t("done")}
          </Button>
        ) : (
          <>
            <Button variant="outline" onClick={onClose}>
              {busy && !checking ? t("background") : checking || failed ? t("close") : t("later")}
            </Button>
            {!busy ? (
              <Button className="min-w-24" disabled={pending} onClick={install}>
                {pending ? (
                  <HugeiconsIcon
                    icon={Loading03Icon}
                    aria-hidden="true"
                    className="motion-safe:animate-spin"
                  />
                ) : null}
                {failed ? t("retry") : t("agreeInstall")}
              </Button>
            ) : null}
          </>
        )}
      </div>
    </>
  );
}
