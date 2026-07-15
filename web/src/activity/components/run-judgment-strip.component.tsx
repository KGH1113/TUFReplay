import type { ActivityJudgmentCounts } from "../activity.model";
import { judgmentDisplayItems } from "../lib/judgment-display";

export function RunJudgmentStrip({ counts }: { counts?: ActivityJudgmentCounts | null }) {
  return (
    <div className="mt-2.5 grid grid-cols-[repeat(9,minmax(0,1fr))] border-t border-border/70 pt-2">
      {judgmentDisplayItems(counts).map(({ key, label, color, value }) => (
        <span
          key={key}
          title={`${label}: ${value}`}
          className="min-w-0 text-center font-heading text-[11px] font-semibold tabular-nums"
          style={{ color }}
        >
          <span className="sr-only">{label}: </span>
          {value}
        </span>
      ))}
    </div>
  );
}
