import { useTranslation } from "react-i18next";
import type { ActivityRun } from "@/models/activity/activity-model";
import { judgmentDisplayItems } from "@/models/activity/judgment-display";
import { cn } from "@/shared/lib/cn";

/** Compact colored counts for a table row; screen readers get every judgment by name. */
export function JudgmentStrip({
  run,
  competitiveLayout = false,
  className,
}: {
  run: Pick<ActivityRun, "judgmentCounts" | "judgmentSystem">;
  /** Columns follow the competitive system; a classic Perfect spans Perfect−, X-Perfect, Perfect+. */
  competitiveLayout?: boolean;
  className?: string;
}) {
  const { t } = useTranslation("activity");
  const items = judgmentDisplayItems(run.judgmentCounts, run.judgmentSystem);
  const spanPerfect = competitiveLayout && run.judgmentSystem !== "ModernCompetitive";
  const description = items.map((item) => `${t(`judgments.${item.key}`)} ${item.value}`).join(", ");
  return (
    <span
      role="img"
      aria-label={description}
      className={cn(
        "judgment-strip grid min-w-0 items-center font-medium tabular-nums [grid-template-columns:var(--judgment-columns)]",
        className,
      )}
    >
      {items.map((item) => (
        <span
          key={item.key}
          aria-hidden="true"
          className={cn(
            "min-w-0 text-center text-[12px] leading-none",
            item.value === 0 && "opacity-35",
            spanPerfect && item.key === "Perfect" && "col-span-3",
          )}
          style={{ color: item.color }}
        >
          {item.value}
        </span>
      ))}
    </span>
  );
}
