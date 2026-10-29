import { useTranslation } from "react-i18next";
import { useRunJudgmentTimeline } from "@/hooks/activity/use-run-judgment-timeline";
import {
  judgmentTimelineColumns,
  timelineJudgmentColor,
} from "@/models/activity/judgment-timeline";
import { cn } from "@/shared/lib/cn";

const COLUMNS = 160;

/**
 * Where the attempt's mistakes happened, start to end, like the in-game replay timeline but
 * without Perfect marks. Each column shows its most severe judgment.
 */
export function JudgmentTimeline({
  runId,
  enabled,
  className,
}: {
  runId: string;
  enabled: boolean;
  className?: string;
}) {
  const { t } = useTranslation("activity");
  const { timeline, loading } = useRunJudgmentTimeline(runId, enabled);
  const columns = timeline ? judgmentTimelineColumns(timeline, COLUMNS) : null;
  const label = timeline
    ? t("attempts.timelineLabel", { count: timeline.kinds.length })
    : t("attempts.timelineUnavailable");

  return (
    <span
      role="img"
      aria-label={label}
      aria-busy={loading}
      className={cn(
        "relative block h-3.5 w-full overflow-hidden rounded-[3px] bg-foreground/[0.06]",
        className,
      )}
    >
      {columns ? (
        <svg
          aria-hidden="true"
          viewBox={`0 0 ${COLUMNS} 1`}
          preserveAspectRatio="none"
          className="absolute inset-0 size-full"
        >
          {columns.map((kind, index) =>
            kind ? (
              <rect
                // biome-ignore lint/suspicious/noArrayIndexKey: columns are fixed positions on the track.
                key={index}
                x={index}
                y={0}
                width={1}
                height={1}
                fill={timelineJudgmentColor(kind)}
              />
            ) : null,
          )}
        </svg>
      ) : null}
    </span>
  );
}
