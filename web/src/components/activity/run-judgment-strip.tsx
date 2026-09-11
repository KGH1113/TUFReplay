import { useTranslation } from "react-i18next";
import type { JudgmentCounts } from "@/models/activity/activity-model";
import { judgmentDisplayItems } from "@/models/activity/judgment-display";

export function RunJudgmentStrip({
  counts,
  judgmentSystem,
}: {
  counts?: JudgmentCounts | null;
  judgmentSystem?: "Legacy" | "ModernClassic" | "ModernCompetitive";
}) {
  const { t } = useTranslation("activity");
  const items = judgmentDisplayItems(counts, judgmentSystem);
  return (
    <div
      className="mt-2.5 grid border-t border-border/70 pt-2"
      style={{ gridTemplateColumns: `repeat(${items.length}, minmax(0, 1fr))` }}
    >
      {items.map(({ key, color, value }) => {
        const label = t(`judgments.${key}`);
        return (
          <span
            key={key}
            title={`${label}: ${value}`}
            className="min-w-0 text-center font-heading text-xs font-semibold tabular-nums"
            style={{ color }}
          >
            <span className="sr-only">{label}: </span>
            {value}
          </span>
        );
      })}
    </div>
  );
}
