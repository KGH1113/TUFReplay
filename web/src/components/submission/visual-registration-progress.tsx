import { Loading03Icon, Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import type { VisualRegistrationProgress } from "@/models/visual/visual-registration-model";
import { cn } from "@/shared/lib/cn";

const steps = ["reading_settings", "processing_assets", "validating", "uploading"] as const;

export function VisualRegistrationProgressView({
  progress,
  status,
}: {
  progress: VisualRegistrationProgress;
  status: "working" | "needs_assets" | "failed";
}) {
  const { t } = useTranslation("submission");
  const active = progress.stage === "preparing" ? -1 : steps.indexOf(progress.stage);
  const working = status === "working";
  return (
    <section
      aria-label={t("visual.progress.title")}
      className="space-y-3 rounded-xl bg-muted/30 p-3 ring-1 ring-border"
    >
      <div role="status" aria-live="polite" aria-atomic="true" className="space-y-1">
        <p className="flex items-center gap-2 text-sm font-medium">
          {working && (
            <HugeiconsIcon
              aria-hidden="true"
              icon={Loading03Icon}
              className="size-4 motion-safe:animate-spin"
            />
          )}
          {working ? t(`visual.progress.${progress.stage}`) : t(`visual.progress.${status}`)}
        </p>
        {working && progress.asset_name && (
          <p className="break-all text-xs text-muted-foreground">{progress.asset_name}</p>
        )}
        {progress.completed_assets > 0 && (
          <p className="text-xs text-muted-foreground">
            {t("visual.progress.assetCount", { count: progress.completed_assets })}
          </p>
        )}
      </div>
      <ol className="grid grid-cols-2 gap-x-4 gap-y-2 text-xs">
        {steps.map((step, index) => {
          const complete = index < active;
          const current = index === active;
          const state = complete
            ? "done"
            : current
              ? working
                ? "active"
                : status === "needs_assets"
                  ? "needs_files"
                  : "stopped"
              : "waiting";
          return (
            <li
              key={step}
              aria-current={current ? "step" : undefined}
              className={cn(
                "flex items-start gap-2 text-muted-foreground",
                current && "text-foreground",
              )}
            >
              {complete ? (
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={Tick02Icon}
                  className="size-4 shrink-0 text-primary"
                />
              ) : (
                <span aria-hidden="true" className="w-4 shrink-0 text-center tabular-nums">
                  {index + 1}
                </span>
              )}
              <span>
                {t(`visual.progress.steps.${step}`)}
                <span className="block text-[11px] text-muted-foreground">
                  {t(`visual.progress.states.${state}`)}
                </span>
              </span>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
