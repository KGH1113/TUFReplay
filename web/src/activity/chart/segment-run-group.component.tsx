import type { MouseEvent } from "react";

import type {
  ActivityLevelSession,
  ActivityRun,
  ActivitySegmentGroup,
  AdaptiveTimeScale,
} from "../activity.model";
import {
  getElapsedMinutes,
  getGroupWidthPercent,
  getRunLeftWithinGroup,
  getRunWidthWithinGroup,
  tileToPercent,
  type ChartMetrics,
} from "../lib/activity-chart.utils";
import { cn } from "@/ui/ui-class.utils";

interface SegmentRunGroupProps {
  activeRunGroupIndex: number | null;
  activeRunId: string | null;
  chart: ChartMetrics;
  group: ActivitySegmentGroup;
  levelSession: ActivityLevelSession;
  pinnedRunId: string | null;
  runs: ActivityRun[];
  selected: boolean;
  timeScale: AdaptiveTimeScale;
  onRunClick: (run: ActivityRun, event: MouseEvent<HTMLButtonElement>) => void;
  onRunMouseEnter: (run: ActivityRun, event: MouseEvent<HTMLButtonElement>) => void;
  onRunMouseLeave: () => void;
  onRunMouseMove: (event: MouseEvent<HTMLButtonElement>) => void;
}

export function SegmentRunGroup(props: SegmentRunGroupProps) {
  const groupStartElapsed = getElapsedMinutes(props.group.FirstStartedAtUtc, props.levelSession.OpenedAtUtc);
  const groupStartPosition = props.timeScale.toPosition(groupStartElapsed);
  const top = (props.timeScale.toPercent(groupStartElapsed) / 100) * props.chart.plotHeight;
  const left = (tileToPercent(props.group.StartTile, props.levelSession.LevelTileCount) / 100) * props.chart.plotWidth;
  const width = (getGroupWidthPercent(props.group, props.levelSession.LevelTileCount) / 100) * props.chart.plotWidth;
  const runTops = props.runs.map((run) =>
    ((props.timeScale.toPosition(getElapsedMinutes(run.StartedAtUtc, props.levelSession.OpenedAtUtc)) - groupStartPosition) /
      (props.timeScale.domain.max - props.timeScale.domain.min)) *
      props.chart.plotHeight +
    12,
  );
  const height = Math.max(48, (Math.max(...runTops, 0) || 0) + 32);

  return (
    <div
      className={cn("absolute block text-left transition", props.selected ? "z-20" : "z-10 hover:z-30")}
      style={{ top, left, width, height, minWidth: 112 }}
    >
      <span
        className={cn(
          "absolute -left-2 top-0 h-full border-l-2",
          props.activeRunGroupIndex === props.group.SegmentGroupIndex
            ? "border-primary"
            : props.selected
              ? "border-muted-foreground"
              : "border-muted-foreground/40",
        )}
      />
      {props.runs.map((run, runIndex) => (
        <button
          key={run.Id}
          type="button"
          className={cn(
            "absolute h-5 rounded-sm border bg-background shadow-sm transition hover:border-primary hover:bg-primary/20",
            props.activeRunId === run.Id
              ? "border-primary bg-primary/25"
              : "border-muted-foreground/60 bg-muted/70 hover:border-primary/70",
          )}
          style={{
            top: runTops[runIndex] ?? runIndex * 24 + 12,
            left: `${getRunLeftWithinGroup(run, props.group, props.levelSession.LevelTileCount)}%`,
            width: `${getRunWidthWithinGroup(run, props.group, props.levelSession.LevelTileCount)}%`,
          }}
          onClick={(event) => props.onRunClick(run, event)}
          onMouseEnter={(event) => props.onRunMouseEnter(run, event)}
          onMouseMove={props.onRunMouseMove}
          onMouseLeave={props.onRunMouseLeave}
          aria-label={`Run ${run.RunIndex}, ${run.StartTile} to ${run.LastTile}`}
        />
      ))}
    </div>
  );
}
