import { Camera01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useId, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useWebcamSettings } from "@/hooks/webcam/use-webcam-settings";
import type { ConnectionStatus } from "@/models/activity/activity-model";
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
import { CameraSettingHelp } from "./camera-setting-help";
import { CameraSettings } from "./camera-settings";

export function WebcamControls({ connectionStatus }: { connectionStatus: ConnectionStatus }) {
  const { t } = useTranslation("webcam");
  const descriptionId = useId();
  const dialogSurface = useRef<HTMLDivElement>(null);
  const camera = useWebcamSettings(connectionStatus);
  const captureDisabled =
    !camera.state || camera.saving || !camera.state.supported || camera.state.captureLocked;
  return (
    <Dialog open={camera.open} onOpenChange={camera.setOpen}>
      <DialogTrigger asChild>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={t("title")}
          title={t("title")}
          disabled={connectionStatus !== "online"}
        >
          <HugeiconsIcon aria-hidden="true" icon={Camera01Icon} size={17} strokeWidth={2} />
        </Button>
      </DialogTrigger>
      <DialogContent
        ref={dialogSurface}
        tabIndex={-1}
        aria-describedby={descriptionId}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          dialogSurface.current?.focus({ preventScroll: true });
        }}
        className="flex h-[min(42rem,calc(100svh-2rem))] w-[calc(100vw-2rem)] flex-col gap-4 overflow-hidden sm:max-w-[58rem]"
      >
        <DialogHeader className="shrink-0 border-b border-border/60 pb-4">
          <div className="flex items-center justify-between gap-4">
            <div className="flex min-w-0 items-center gap-1">
              <DialogTitle>{t("title")}</DialogTitle>
              <CameraSettingHelp label={t("title")}>{t("permissionHint")}</CameraSettingHelp>
            </div>
            <Switch
              aria-label={t("enabled")}
              aria-describedby={descriptionId}
              className="shrink-0"
              checked={camera.state?.enabled ?? false}
              disabled={captureDisabled}
              onCheckedChange={(enabled) => camera.update({ enabled })}
            />
          </div>
          <DialogDescription id={descriptionId} className="sr-only">
            {t("permissionHint")}
          </DialogDescription>
        </DialogHeader>
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
        <div className="flex shrink-0 items-center justify-between gap-3 border-t border-border/60 pt-4">
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
