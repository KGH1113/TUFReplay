import { Download04Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { RenderOptionsForm } from "@/components/render/render-options-form";
import { useDownloads } from "@/hooks/downloads/use-downloads";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";

export function RenderDialog({ control }: { control: ReturnType<typeof useRenderControl> }) {
  const { t, i18n } = useTranslation("render");
  const downloads = useDownloads(Boolean(control.run));
  const unavailable =
    control.health && (!control.health.orbitAvailable || !control.health.available);
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
  const needsRendererSetup = control.errorCode === "renderer_missing" && !control.settings;
  const rendererState = downloads.state?.Renderer.Status;
  const setupStep =
    rendererState === "restart-required"
      ? "restart"
      : rendererState === "ready"
        ? "ready"
        : "install";
  const canStart =
    !control.busy &&
    control.phase !== "checking" &&
    !control.choosingDirectory &&
    !unavailable &&
    !control.health?.busy &&
    Boolean(control.health && control.settings) &&
    control.optionValidation.success &&
    control.options.outputDirectory.trim().length > 0;

  if (needsRendererSetup) {
    return (
      <Dialog
        open={Boolean(control.run)}
        onOpenChange={(open) => {
          if (!open) control.close();
        }}
      >
        <DialogContent className="w-[min(30rem,calc(100vw-2rem))] space-y-6 p-6">
          <div className="flex size-11 items-center justify-center rounded-2xl bg-muted/60 text-muted-foreground">
            <HugeiconsIcon icon={Download04Icon} size={22} aria-hidden="true" />
          </div>
          <DialogHeader>
            <DialogTitle>{t(`setup.${setupStep}.title`)}</DialogTitle>
            <DialogDescription className="leading-relaxed">
              {t(`setup.${setupStep}.description`)}
            </DialogDescription>
          </DialogHeader>
          {setupStep === "install" ? (
            <p className="text-xs leading-relaxed text-muted-foreground">
              {t("setup.restartHint")}
            </p>
          ) : null}
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="ghost" onClick={control.close}>
              {t("setup.later")}
            </Button>
            <Button
              disabled={downloads.pending}
              onClick={() => {
                if (setupStep === "install") void downloads.act("renderer", "request");
                else if (control.run) control.open(control.run, undefined, control.levelPath);
              }}
            >
              {setupStep === "install"
                ? t("installRenderer")
                : setupStep === "ready"
                  ? t("setup.continue")
                  : t("setup.checkConnection")}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    );
  }

  return (
    <Dialog
      open={Boolean(control.run)}
      onOpenChange={(open) => {
        if (!open) control.close();
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
          {!completed && control.settings ? <RenderOptionsForm control={control} /> : null}
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
              <p className="font-medium">
                {control.busy && control.job?.waitingForFfmpeg
                  ? t("waitingForFfmpeg")
                  : control.busy && control.job?.waitingForGameFocus
                    ? t("waitingForGameFocus")
                    : t(`status.${control.phase}`)}
              </p>
            ) : null}
            {control.busy && control.phase !== "checking" && control.phase !== "installing" ? (
              <progress
                aria-label={t("progress")}
                value={control.progress}
                max={1}
                className="h-2 w-full accent-primary"
              />
            ) : null}
            {control.busy ? (
              <p className="text-xs text-muted-foreground">
                {control.job?.waitingForFfmpeg
                  ? t("ffmpegInstallHelp")
                  : control.job?.waitingForGameFocus
                    ? t("gameFocusWaitHelp")
                    : t("keepGameOpen")}
              </p>
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
                {control.errorCode === "renderer_missing" &&
                downloads.state?.Renderer.Status !== "restart-required" ? (
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={downloads.pending}
                    onClick={() => downloads.act("renderer", "request")}
                  >
                    {t("installRenderer")}
                  </Button>
                ) : null}
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
          {completed ? (
            <Button
              disabled={!control.job?.canOpenOutput || control.openingDirectory}
              onClick={() => void control.openOutputDirectory()}
            >
              {control.openingDirectory ? t("openingDirectory") : t("openSaveLocation")}
            </Button>
          ) : control.phase === "failed" && !control.health ? (
            <Button
              onClick={() => {
                if (control.run) control.open(control.run, undefined, control.levelPath);
              }}
            >
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
