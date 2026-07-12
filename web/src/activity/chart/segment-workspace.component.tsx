import { useEffect, useState, type MouseEvent } from "react";

import type { ActivityLevelSession, ActivityRun, ActivitySegmentGroup } from "../activity.model";
import { getLevelSessionRuns, getTimeDomain } from "../lib/activity-selectors";
import {
  getAdaptiveTimeScale,
  getTimeTicks,
  getTooltipPosition,
} from "../lib/activity-chart.utils";
import { RunTooltip } from "../components/run-tooltip.component";
import { ScrollHint } from "../components/scroll-hint.component";
import { RunChartGrid } from "./run-chart-grid.component";
import { SegmentRunGroup } from "./segment-run-group.component";
import { useRunChartViewport } from "./use-run-chart-viewport.hook";

interface SegmentWorkspaceProps {
  levelSession: ActivityLevelSession;
  segmentGroups: ActivitySegmentGroup[];
  selectedSegmentGroupIndex: number;
  onSelectSegmentGroup: (index: number) => void;
}

export function SegmentWorkspace({
  levelSession,
  segmentGroups,
  selectedSegmentGroupIndex,
  onSelectSegmentGroup,
}: SegmentWorkspaceProps) {
  const [hoveredRunId, setHoveredRunId] = useState<string | null>(null);
  const [pinnedRunId, setPinnedRunId] = useState<string | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ left: 0, top: 0 });
  const runs = getLevelSessionRuns(levelSession);
  const plottedGroups = segmentGroups.map((group) => ({
    group,
    runs: runs.filter((run) => run.SegmentGroupIndex === group.SegmentGroupIndex),
  }));
  const timeDomain = getTimeDomain(levelSession, runs);
  const timeScale = getAdaptiveTimeScale(levelSession, plottedGroups, timeDomain);
  const viewport = useRunChartViewport({
    domain: timeScale.domain,
    levelSessionId: levelSession.Id,
    selectedSegmentGroupIndex,
    timeDomainMax: timeDomain.max,
  });
  const chart = viewport.chart;
  const timeTicks = getTimeTicks(timeDomain, timeScale);
  const activeRun = runs.find((run) => run.Id === (pinnedRunId ?? hoveredRunId)) ?? null;
  const activeRunId = pinnedRunId ?? hoveredRunId;
  const activeRunGroupIndex = activeRun?.SegmentGroupIndex ?? null;

  useEffect(() => {
    setHoveredRunId(null);
    setPinnedRunId(null);
  }, [levelSession.Id]);

  const handleRunClick = (run: ActivityRun, event: MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
    setTooltipPosition(getTooltipPosition(event));
    setPinnedRunId((current) => (current === run.Id ? null : run.Id));
    setHoveredRunId(null);
    onSelectSegmentGroup(run.SegmentGroupIndex);
  };

  const handleRunMouseEnter = (run: ActivityRun, event: MouseEvent<HTMLButtonElement>) => {
    if (pinnedRunId) return;
    setTooltipPosition(getTooltipPosition(event));
    setHoveredRunId(run.Id);
  };

  const handleRunMouseMove = (event: MouseEvent<HTMLButtonElement>) => {
    if (!pinnedRunId) setTooltipPosition(getTooltipPosition(event));
  };

  return (
    <section className="flex min-h-0 min-w-0 flex-1 flex-col px-3 pb-3 pt-2">
      <div className="mb-2 flex shrink-0 flex-col gap-2 md:flex-row md:items-end md:justify-between">
        <h2 className="font-heading text-lg font-semibold">Runs</h2>
      </div>
      <div className="relative min-h-0 flex-1 overflow-hidden border border-border bg-background">
        <div ref={viewport.viewportRef} className="absolute inset-0 overflow-auto" onScroll={viewport.handleScroll}>
          <div className="relative" style={{ width: chart.contentWidth, height: chart.contentHeight }}>
            <RunChartGrid chart={chart} timeScale={timeScale} timeTicks={timeTicks} />
            <div
              className="absolute"
              style={{ left: chart.leftAxis, top: chart.topAxis, width: chart.plotWidth, height: chart.plotHeight }}
            >
              {plottedGroups.map(({ group, runs: groupRuns }) => (
                <SegmentRunGroup
                  key={`${group.SegmentGroupIndex}-${group.StartTile}`}
                  activeRunGroupIndex={activeRunGroupIndex}
                  activeRunId={activeRunId}
                  chart={chart}
                  group={group}
                  levelSession={levelSession}
                  pinnedRunId={pinnedRunId}
                  runs={groupRuns}
                  selected={selectedSegmentGroupIndex === group.SegmentGroupIndex}
                  timeScale={timeScale}
                  onRunClick={handleRunClick}
                  onRunMouseEnter={handleRunMouseEnter}
                  onRunMouseLeave={() => {
                    if (!pinnedRunId) setHoveredRunId(null);
                  }}
                  onRunMouseMove={handleRunMouseMove}
                />
              ))}
              {activeRun ? (
                <RunTooltip
                  run={activeRun}
                  pinned={pinnedRunId === activeRun.Id}
                  style={tooltipPosition}
                  onClose={() => setPinnedRunId(null)}
                />
              ) : null}
            </div>
          </div>
        </div>
        <ScrollHint direction="left" visible={viewport.scrollState.canScrollLeft} />
        <ScrollHint direction="right" visible={viewport.scrollState.canScrollRight} />
        <ScrollHint direction="up" visible={viewport.scrollState.canScrollUp} />
        <ScrollHint direction="down" visible={viewport.scrollState.canScrollDown} />
      </div>
    </section>
  );
}
