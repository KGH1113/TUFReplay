import { useTranslation } from "react-i18next";
import { RenderOptionsForm } from "@/components/render/render-options-form";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Progress } from "@/shared/ui/progress";

export function RenderDialog({ control }: { control: ReturnType<typeof useRenderControl> }) {
  const { t, i18n } = useTranslation("render");
  const unavailable = control.health ? !control.health.orbitAvailable : false;
  const knownError =
    control.errorCode && i18n.exists(`errors.${control.errorCode}`, { ns: "render" })
      ? t(`errors.${control.errorCode}` as never)
      : null;
  const field = control.errorDetails?.field?.replace(/^replay\./, "");
  const fieldLabel =
    field && i18n.exists(`errorFields.${field}`, { ns: "render" })
      ? t(`errorFields.${field}` as never)
      : field && i18n.exists(`options.${field}`, { ns: "render" })
        ? t(`options.${field}` as never)
        : field;
  const completed = control.phase === "completed";
  const canStart =
    !control.busy &&
    control.phase !== "checking" &&
    !unavailable &&
    !control.health?.busy &&
    Boolean(control.health?.orbitAvailable) &&
    control.optionValidation.success;

  return (
    <Dialog
      open={Boolean(control.run)}
      onOpenChange={(open) => {
        if (!open && !control.busy) control.close();
      }}
    >
      <DialogContent
        className="flex max-h-[min(90dvh,56rem)] w-[min(44rem,calc(100vw-2rem))] flex-col gap-5 p-0"
        onEscapeKeyDown={(event) => {
          if (control.busy) event.preventDefault();
        }}
        onInteractOutside={(event) => {
          if (control.busy) event.preventDefault();
        }}
      >
        <DialogHeader className="shrink-0 px-6 pt-6">
          <DialogTitle>{t("title", { runIndex: control.run?.runIndex })}</DialogTitle>
          <DialogDescription>
            {completed ? t("completedDescription") : t("description")}
          </DialogDescription>
        </DialogHeader>
        <div className="min-h-0 space-y-5 overflow-y-auto px-6 pb-1">
          {!completed && !control.busy ? <RenderOptionsForm control={control} /> : null}
          <div aria-live="polite" className="space-y-2 text-sm">
            {control.phase !== "idle" ? (
              <p className="font-medium">{t(`status.${control.phase}`)}</p>
            ) : null}
            {control.busy ? (
              <Progress
                aria-label={t("progress")}
                value={Math.max(0, Math.min(100, control.progress * 100))}
                className="h-2"
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
            {control.errorMessage || knownError ? (
              <div role="alert" className="space-y-2 rounded-lg border border-border p-3">
                <p>{knownError || control.errorMessage || t("errors.failed")}</p>
                {knownError && control.errorMessage && control.errorMessage !== knownError ? (
                  <details className="text-xs text-muted-foreground">
                    <summary className="cursor-pointer">{t("errorDetails")}</summary>
                    <p className="mt-2 break-words leading-relaxed">{control.errorMessage}</p>
                  </details>
                ) : null}
                {fieldLabel ? (
                  <p className="text-xs">{t("errorDetailField", { field: fieldLabel })}</p>
                ) : null}
                {control.errorDetails?.line ? (
                  <p className="text-xs">
                    {t("errorDetailLine", {
                      line: control.errorDetails.line,
                      file: control.errorDetails.file || t("recordingFile"),
                    })}
                  </p>
                ) : null}
              </div>
            ) : null}
            {completed && (control.job?.localOutputPath || control.job?.outputFile) ? (
              <div className="rounded-lg bg-muted/40 p-3">
                <p className="mb-1 text-xs font-medium">{t("savedTo")}</p>
                <p className="break-all text-xs leading-relaxed text-muted-foreground">
                  {control.job.localOutputPath || control.job.outputFile}
                </p>
              </div>
            ) : null}
          </div>
        </div>
        <div className="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/20 px-6 py-4">
          <Button
            variant="outline"
            onClick={control.busy ? () => void control.cancel() : control.close}
          >
            {control.busy ? t("cancel") : t("close")}
          </Button>
          {completed ? null : control.phase === "failed" ? (
            <Button disabled={!canStart} onClick={() => void control.start()}>
              {t("retry")}
            </Button>
          ) : (
            <Button disabled={!canStart} onClick={() => void control.start()}>
              {t("start")}
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
