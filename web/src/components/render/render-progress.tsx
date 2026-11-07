import { useTranslation } from "react-i18next";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import { Progress } from "@/shared/ui/progress";

type Control = ReturnType<typeof useRenderControl>;

/** The video being made: what is happening now and how far along it is. */
export function RenderProgress({
  phase,
  progress,
  job,
}: {
  phase: Control["phase"];
  progress: number;
  job: Control["job"];
}) {
  const { t } = useTranslation("render");
  const percent = Math.round(Math.max(0, Math.min(1, progress)) * 100);
  return (
    <div className="space-y-3 py-2">
      <div aria-live="polite" className="flex items-baseline justify-between gap-4">
        <p className="text-base font-semibold">{t(`status.${phase}`)}</p>
        <span className="text-sm tabular-nums text-muted-foreground">
          {t("progress.percent", { percent })}
        </span>
      </div>
      <Progress aria-label={t("progress.label")} value={percent} className="h-2" />
      <p className="text-xs leading-relaxed text-muted-foreground">
        {job?.waitingForGameFocus ? t("progress.focusGame") : t("progress.keepOpen")}
      </p>
    </div>
  );
}
