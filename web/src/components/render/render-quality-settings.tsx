import { useId } from "react";
import { useTranslation } from "react-i18next";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import { cn } from "@/shared/lib/cn";

export function RenderQualitySettings({
  control,
}: {
  control: ReturnType<typeof useRenderControl>;
}) {
  const { t } = useTranslation("render");
  const id = useId();
  const system = control.settings?.system;
  const selected = control.recommendations.levels.find(
    (value) => value.quality === control.quality,
  );
  const disabled = control.busy || control.phase === "checking";
  const discovery = !system
    ? "legacy"
    : system.encoding.state === "checking"
      ? "checking"
      : system.encoding.state === "missing"
        ? "needsFfmpeg"
        : system.encoding.state === "unavailable"
          ? "unavailable"
          : control.recommendations.hardware
            ? "hardware"
            : "software";

  return (
    <div className="space-y-4">
      <fieldset disabled={disabled} className="space-y-3">
        <legend className="text-sm font-semibold">{t("recommendation.quality")}</legend>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {control.recommendations.levels.map((level) => (
            <label
              key={level.quality}
              className={cn(
                "relative flex min-h-20 cursor-pointer flex-col justify-center gap-1 rounded-xl border px-3 py-3 transition-colors focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2 focus-within:ring-offset-background",
                control.quality === level.quality
                  ? "border-primary/50 bg-primary/[0.07]"
                  : "border-border hover:bg-muted/40",
                (!level.supported || disabled) && "cursor-default opacity-50",
              )}
            >
              <input
                type="radio"
                name={`${id}-quality`}
                value={level.quality}
                checked={control.quality === level.quality}
                disabled={!level.supported || disabled}
                onChange={() => control.selectQuality(level.quality)}
                className="sr-only"
              />
              <span className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-medium">
                {t(`qualities.${level.quality}`)}
                {level.quality === control.recommendations.recommended ? (
                  <span className="text-[0.65rem] font-medium text-primary">
                    {t("recommendation.badge")}
                  </span>
                ) : null}
              </span>
              <span className="text-xs text-muted-foreground">
                {t("recommendation.resolution", {
                  height: level.video.height,
                  fps: level.video.videoFps,
                })}
              </span>
            </label>
          ))}
        </div>
      </fieldset>
      {selected ? (
        <div
          aria-live="polite"
          className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 text-xs text-muted-foreground"
        >
          <span>
            {t(`encoders.${selected.video.encoder}`)} · {selected.video.bitrateMbps} Mbps
          </span>
          <span>{t("recommendation.size", { size: selected.megabytesPerMinute })}</span>
        </div>
      ) : null}
      <div className="space-y-1.5 text-xs leading-relaxed text-muted-foreground">
        <p>{t(`recommendation.${discovery}`)}</p>
        {control.quality === "highest" || control.quality === "extreme" ? (
          <p>{t("recommendation.heavy")}</p>
        ) : null}
        {control.recommendations.levels.some((value) => !value.supported) ? (
          <p>{t("recommendation.textureLimit")}</p>
        ) : null}
      </div>
      {system ? (
        <details className="border-t border-border pt-3 text-xs text-muted-foreground">
          <summary className="w-fit cursor-pointer rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
            {t("recommendation.systemDetails")}
          </summary>
          <div className="mt-2 space-y-1 break-words leading-relaxed">
            <p>{system.processorName || t("recommendation.unknownProcessor")}</p>
            <p>{system.graphicsName || t("recommendation.unknownGraphics")}</p>
            <p>
              {t("recommendation.memory", {
                size: Math.round(system.memoryMb / 1024),
                threads: system.logicalProcessors,
              })}
            </p>
          </div>
        </details>
      ) : null}
    </div>
  );
}
