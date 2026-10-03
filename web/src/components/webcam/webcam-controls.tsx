import { Camera01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
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
import { CameraSettings } from "./camera-settings";

export function WebcamControls({ connectionStatus }: { connectionStatus: ConnectionStatus }) {
  const { t } = useTranslation("webcam");
  const camera = useWebcamSettings(connectionStatus);
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
      <DialogContent className="max-h-[calc(100svh_-_2rem)] w-[calc(100vw_-_2rem)] overflow-y-auto sm:max-w-[58rem] [scrollbar-width:thin] [scrollbar-color:var(--color-border)_transparent]">
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
          <DialogDescription>{t("description")}</DialogDescription>
        </DialogHeader>
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
        <div className="flex items-center justify-between gap-3 border-t border-border/60 pt-4">
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
