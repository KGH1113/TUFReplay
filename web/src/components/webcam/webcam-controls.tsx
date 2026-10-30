import { Cancel01Icon, Video01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useId, useRef } from "react";
import { useTranslation } from "react-i18next";
import { SidebarSettingButton } from "@/components/shell/sidebar-setting-button";
import { useWebcamSettings } from "@/hooks/webcam/use-webcam-settings";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/shared/ui/dialog";
import { Switch } from "@/shared/ui/switch";
import { CameraSettings } from "./camera-settings";

export function WebcamControls({ connectionStatus }: { connectionStatus: ConnectionStatus }) {
  const { t } = useTranslation("webcam");
  const { t: commonT } = useTranslation("common");
  const descriptionId = useId();
  const switchId = useId();
  const dialogSurface = useRef<HTMLDivElement>(null);
  const camera = useWebcamSettings(connectionStatus);
  const captureDisabled =
    !camera.state || camera.saving || !camera.state.supported || camera.state.captureLocked;
  return (
    <Dialog open={camera.open} onOpenChange={camera.setOpen}>
      <DialogTrigger asChild>
        <SidebarSettingButton
          icon={Video01Icon}
          label={commonT("settings.camera")}
          value={
            connectionStatus !== "online"
              ? commonT("settings.needsConnection")
              : camera.state
                ? t(`status.${camera.state.status}`)
                : commonT("settings.openToCheck")
          }
          attention={
            connectionStatus === "online" &&
            (camera.state?.status === "error" || Boolean(camera.state?.captureError))
          }
          aria-label={t("title")}
          disabled={connectionStatus !== "online"}
        />
      </DialogTrigger>
      <DialogContent
        ref={dialogSurface}
        tabIndex={-1}
        aria-describedby={descriptionId}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          dialogSurface.current?.focus({ preventScroll: true });
        }}
        className="flex h-[min(46rem,calc(100svh-2rem))] w-[calc(100vw-2rem)] flex-col gap-4 overflow-hidden p-6 sm:max-w-[58rem]"
      >
        <DialogHeader className="shrink-0 space-y-0">
          <div className="flex items-center justify-between gap-4">
            <DialogTitle className="text-lg">{t("title")}</DialogTitle>
            <Button
              type="button"
              size="icon-sm"
              variant="ghost"
              className="-mr-1 text-muted-foreground"
              aria-label={t("done")}
              onClick={() => camera.setOpen(false)}
            >
              <HugeiconsIcon aria-hidden="true" icon={Cancel01Icon} className="size-4" />
            </Button>
          </div>
          <DialogDescription id={descriptionId} className="sr-only">
            {t("permissionHint")}
          </DialogDescription>
        </DialogHeader>
        <label
          htmlFor={switchId}
          className={cn(
            "flex shrink-0 cursor-pointer items-center gap-3 rounded-2xl border p-3.5 transition-colors",
            camera.state?.enabled
              ? "border-primary/25 bg-primary/[0.07]"
              : "border-border bg-foreground/[0.03]",
            captureDisabled && "cursor-default",
          )}
        >
          <span
            className={cn(
              "grid size-10 shrink-0 place-items-center rounded-xl",
              camera.state?.enabled
                ? "bg-primary text-primary-foreground"
                : "bg-foreground/[0.07] text-muted-foreground",
            )}
          >
            <HugeiconsIcon aria-hidden="true" icon={Video01Icon} size={20} strokeWidth={1.8} />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block text-sm font-semibold">{t("recordingSwitch")}</span>
            <span className="block text-xs leading-relaxed text-muted-foreground">
              {camera.state ? `${t(`status.${camera.state.status}`)} · ` : ""}
              {t("permissionHint")}
            </span>
          </span>
          <Switch
            id={switchId}
            aria-label={t("enabled")}
            aria-describedby={descriptionId}
            className="shrink-0"
            checked={camera.state?.enabled ?? false}
            disabled={captureDisabled}
            onCheckedChange={(enabled) => camera.update({ enabled })}
          />
        </label>
        <div className="min-h-0 min-w-0 flex-1 overflow-x-hidden overflow-y-auto overscroll-contain [scrollbar-width:thin] [scrollbar-color:var(--color-border)_transparent]">
          {camera.error ? (
            <p role="alert" className="text-sm text-amber-500">
              {camera.error}
            </p>
          ) : null}
          {camera.loading ? (
            <p role="status" className="text-sm text-muted-foreground">
              {t("loading")}
            </p>
          ) : null}
          {camera.state ? (
            <CameraSettings
              state={camera.state}
              saving={camera.saving}
              previewActive={camera.open && connectionStatus === "online"}
              onUpdate={camera.update}
              onRefresh={camera.refresh}
            />
          ) : null}
        </div>
        <div className="flex shrink-0 items-center justify-between gap-3 border-t border-border pt-4">
          <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
            {camera.saving
              ? t("savingSettings")
              : camera.saved && !camera.error
                ? t("savedSettings")
                : ""}
          </p>
          <Button size="sm" onClick={() => camera.setOpen(false)}>
            {t("done")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
