import type { AdaptiveTimeScale } from "../activity.model";
import {
  formatElapsedMinute,
  getExpandedTileTicks,
  getTileTicks,
  type ChartMetrics,
  type TimeTick,
} from "../lib/activity-chart.utils";
import { cn } from "@/ui/ui-class.utils";

interface RunChartGridProps {
  chart: ChartMetrics;
  timeScale: AdaptiveTimeScale;
  timeTicks: TimeTick[];
}

export function RunChartGrid({ chart, timeScale, timeTicks }: RunChartGridProps) {
  return (
    <>
      <div className="absolute left-0 top-0 z-10 bg-background" style={{ width: chart.leftAxis, height: chart.topAxis }} />
      <div
        className="absolute top-0 z-10 border-b border-border bg-muted/10"
        style={{ left: chart.leftAxis, width: chart.plotWidth, height: chart.topAxis }}
      >
        {getTileTicks().map((percent) => (
          <div
            key={percent}
            className="absolute top-0 flex h-12 -translate-x-1/2 flex-col items-center justify-end gap-1 pb-1 text-xs text-muted-foreground"
            style={{ left: `${percent}%` }}
          >
            <span>{percent}%</span>
            <span className="h-2 border-l border-border" />
          </div>
        ))}
      </div>
      <div
        className="absolute left-0 z-10 border-r border-border bg-muted/10"
        style={{ top: chart.topAxis, width: chart.leftAxis, height: chart.plotHeight }}
      >
        {timeTicks.map((tick) => (
          <div
            key={`${tick.value}-${tick.expanded ? "expanded" : "regular"}`}
            className={cn(
              "absolute right-0 flex -translate-y-1/2 items-center gap-2 pr-2 text-xs",
              tick.expanded ? "text-foreground" : "text-muted-foreground",
            )}
            style={{ top: `${timeScale.toPercent(tick.value)}%` }}
          >
            <span>{formatElapsedMinute(tick.value)}</span>
            <span className={cn("w-2 border-t", tick.expanded ? "border-primary/60" : "border-border")} />
          </div>
        ))}
      </div>
      <div className="pointer-events-none absolute inset-0">
        {getTileTicks().slice(1).map((percent) => (
          <div key={percent} className="absolute inset-y-0 border-l border-border/70" style={{ left: `${percent}%` }} />
        ))}
        {timeScale.expandedRanges.flatMap((range) =>
          getExpandedTileTicks().map((percent) => (
            <div
              key={`${range.start}-${range.end}-${percent}`}
              className="absolute border-l border-border/30"
              style={{
                left: `${percent}%`,
                top: `${timeScale.toPercent(range.start)}%`,
                height: `${timeScale.toPercent(range.end) - timeScale.toPercent(range.start)}%`,
              }}
            />
          )),
        )}
        {timeTicks.map((tick) => (
          <div
            key={`${tick.value}-${tick.expanded ? "expanded" : "regular"}`}
            className={cn("absolute inset-x-0 border-t", tick.expanded ? "border-border/40" : "border-border/50")}
            style={{ top: `${timeScale.toPercent(tick.value)}%` }}
          />
        ))}
      </div>
    </>
  );
}
