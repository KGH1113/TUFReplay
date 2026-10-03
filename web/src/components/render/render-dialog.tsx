import { useId } from "react";
import { useTranslation } from "react-i18next";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Switch } from "@/shared/ui/switch";

export function RenderDialog({ control }: { control: ReturnType<typeof useRenderControl> }) {
  const { t } = useTranslation("render");
  const id = useId();
  const unavailable =
    control.health && (!control.health.orbitAvailable || !control.health.available);
  const error = control.errorCode && t(`errors.${control.errorCode}`, { defaultValue: "" });
  return (
    <Dialog
      open={Boolean(control.run)}
      onOpenChange={(open) => {
        if (!open) control.close();
      }}
    >
      <DialogContent
        onEscapeKeyDown={(event) => {
          if (control.busy) event.preventDefault();
        }}
        onInteractOutside={(event) => {
          if (control.busy) event.preventDefault();
        }}
        className="w-[min(30rem,calc(100vw-2rem))] space-y-5"
      >
        <DialogHeader>
          <DialogTitle>{t("title", { runIndex: control.run?.runIndex })}</DialogTitle>
          <DialogDescription>{t("description")}</DialogDescription>
        </DialogHeader>
        <fieldset disabled={control.busy} className="space-y-4 disabled:opacity-60">
          <div className="grid grid-cols-2 gap-3">
            <label htmlFor={`${id}-size`} className="space-y-1.5 text-sm">
              <span>{t("resolution")}</span>
              <select
                id={`${id}-size`}
                value={`${control.options.width}x${control.options.height}`}
                onChange={(event) => {
                  const [width, height] = event.target.value.split("x").map(Number);
                  control.setOptions((current) => ({ ...current, width, height }));
                }}
                className="h-9 w-full rounded-md border border-input bg-background px-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <option value="1280x720">1280 × 720</option>
                <option value="1920x1080">1920 × 1080</option>
                <option value="2560x1440">2560 × 1440</option>
                <option value="3840x2160">3840 × 2160</option>
              </select>
            </label>
            <label htmlFor={`${id}-fps`} className="space-y-1.5 text-sm">
              <span>{t("frameRate")}</span>
              <select
                id={`${id}-fps`}
                value={control.options.fps}
                onChange={(event) =>
                  control.setOptions((current) => ({
                    ...current,
                    fps: Number(event.target.value) as 30 | 60 | 120,
                  }))
                }
                className="h-9 w-full rounded-md border border-input bg-background px-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <option value="30">30 fps</option>
                <option value="60">60 fps</option>
                <option value="120">120 fps</option>
              </select>
            </label>
          </div>
          {(["includeWebcam", "includeMicrophone", "includeDmNote"] as const).map((option) => (
            <div key={option} className="flex items-center justify-between gap-4">
              <label htmlFor={`${id}-${option}`} className="text-sm">
                {t(option)}
                {option === "includeDmNote" && !control.health?.dmNoteConfigured ? (
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {t("dmNoteUnavailable")}
                  </span>
                ) : null}
              </label>
              <Switch
                id={`${id}-${option}`}
                checked={
                  control.options[option] &&
                  (option !== "includeDmNote" || Boolean(control.health?.dmNoteConfigured))
                }
                disabled={
                  control.busy || (option === "includeDmNote" && !control.health?.dmNoteConfigured)
                }
                onCheckedChange={(checked) =>
                  control.setOptions((current) => ({ ...current, [option]: checked }))
                }
              />
            </div>
          ))}
        </fieldset>
        <div aria-live="polite" className="space-y-2 text-sm">
          {control.job?.warnings.length ? (
            <div className="rounded-lg border border-amber-500/25 bg-amber-500/5 p-3 text-xs">
              <p className="mb-1 font-medium">{t("warnings")}</p>
              <ul className="list-disc space-y-1 pl-4">
                {control.job.warnings.map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            </div>
          ) : null}
          {control.phase !== "idle" ? (
            <p className="font-medium">{t(`status.${control.phase}`)}</p>
          ) : null}
          {control.busy && control.phase !== "checking" ? (
            <progress
              aria-label={t("progress")}
              value={control.progress}
              max={1}
              className="h-2 w-full accent-primary"
            />
          ) : null}
          {control.busy ? (
            <p className="text-xs text-muted-foreground">{t("keepGameOpen")}</p>
          ) : null}
          {unavailable ? (
            <p role="alert" className="text-destructive">
              {t("errors.orbit_missing")}
            </p>
          ) : null}
          {control.health?.busy ? (
            <p role="alert" className="text-destructive">
              {t("errors.render_busy")}
            </p>
          ) : null}
          {control.errorMessage || error ? (
            <p role="alert" className="text-destructive">
              {error || control.errorMessage || t("errors.failed")}
            </p>
          ) : null}
          {control.job?.outputFile && control.phase === "completed" ? (
            <p className="break-all text-xs text-muted-foreground">{control.job.outputFile}</p>
          ) : null}
        </div>
        <div className="flex justify-end gap-2">
          {control.busy ? (
            <Button variant="outline" onClick={() => void control.cancel()}>
              {t("cancel")}
            </Button>
          ) : (
            <Button variant="outline" onClick={control.close}>
              {t("close")}
            </Button>
          )}
          {control.job?.canDownload ? (
            <Button onClick={() => void control.download()}>{t("download")}</Button>
          ) : (
            <Button
              disabled={
                control.busy ||
                Boolean(unavailable) ||
                Boolean(control.health?.busy) ||
                !control.health
              }
              onClick={() => void control.start()}
            >
              {t("start")}
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
