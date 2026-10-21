import { useTranslation } from "react-i18next";
import {
  type DownloadAction,
  type DownloadItemId,
  type InstallState,
  installationBusy,
} from "@/api/downloads/downloads-api";
import { Button } from "@/shared/ui/button";

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
  const consent = state.Status === "awaiting-consent";
  const done = state.Status === "ready" || state.Status === "restart-required";
  const progress = state.TotalBytes ? state.DownloadedBytes / state.TotalBytes : undefined;
  const knownError =
    state.ErrorCode && i18n.exists(`errors.${state.ErrorCode}`, { ns: "downloads" });
  const name = item === "renderer" ? "TUFReplay-Renderer" : "FFmpeg";
  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between gap-3">
        <p role="status" aria-live="polite" className="text-sm font-medium">
          {t(`status.${state.Status}`)}
        </p>
        {state.Version ? (
          <span className="shrink-0 text-xs text-muted-foreground">{state.Version}</span>
        ) : null}
      </div>
      {busy && state.Status !== "checking" ? (
        <div className="space-y-1">
          <progress
            aria-label={t("progress", { name })}
            value={progress}
            max={1}
            className="h-1.5 w-full accent-primary"
          />
          {state.TotalBytes ? (
            <p className="text-right text-xs tabular-nums text-muted-foreground">
              {(state.DownloadedBytes / 1048576).toFixed(1)} /{" "}
              {(state.TotalBytes / 1048576).toFixed(1)} MB
            </p>
          ) : null}
        </div>
      ) : null}
      {consent ? (
        <div className="space-y-3 text-sm leading-relaxed text-muted-foreground">
          <p>{t(`consent.${item}`)}</p>
          <a
            href={
              item === "renderer"
                ? "https://github.com/KGH1113/TUFReplay-Renderer/blob/main/LICENSE.md"
                : "https://ffmpeg.org/legal.html"
            }
            target="_blank"
            rel="noreferrer"
            className="text-primary underline underline-offset-2"
          >
            {t("license")}
          </a>
        </div>
      ) : null}
      {state.InstallationDirectory && (consent || done) ? (
        <p className="break-all text-xs leading-relaxed text-muted-foreground">
          {t("installLocation", { path: state.InstallationDirectory })}
        </p>
      ) : null}
      {state.Status === "restart-required" ? (
        <p className="text-xs leading-relaxed text-muted-foreground">{t("restartHelp")}</p>
      ) : null}
      {state.Status === "failed" ? (
        <div role="alert" className="space-y-1 text-xs leading-relaxed text-destructive">
          <p>{knownError ? t(`errors.${state.ErrorCode}` as never) : t("errors.installFailed")}</p>
          {state.Error ? (
            <details>
              <summary className="cursor-pointer">{t("details")}</summary>
              <p className="mt-1 break-words">{state.Error}</p>
            </details>
          ) : null}
        </div>
      ) : null}
      <div className="flex flex-wrap justify-end gap-2 border-t border-border/60 pt-4">
        <Button size="sm" variant="outline" onClick={onClose}>
          {done
            ? t("done")
            : busy && state.Status !== "checking"
              ? t("background")
              : consent
                ? t("later")
                : t("close")}
        </Button>
        {busy && state.Status !== "checking" ? (
          <Button
            size="sm"
            variant="ghost"
            disabled={pending}
            onClick={() => onAction(item, "cancel")}
          >
            {t("cancel")}
          </Button>
        ) : null}
        {!busy && !done ? (
          <Button
            size="sm"
            disabled={pending}
            onClick={() => onAction(item, consent ? "confirm" : "request")}
          >
            {consent ? t("agreeInstall") : state.Status === "failed" ? t("retry") : t("install")}
          </Button>
        ) : null}
      </div>
    </div>
  );
}
