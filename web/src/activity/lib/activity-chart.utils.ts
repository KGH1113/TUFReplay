import type {
  Dispatch,
  MouseEvent,
  SetStateAction,
} from "react";

import type {
  ActivityLevelSession,
  ActivityRun,
  ActivitySegmentGroup,
  AdaptiveTimeScale,
  PlottedSegmentGroup,
  TimeExpansionRange,
} from "../activity.model";

export interface ChartMetrics {
  leftAxis: number;
  topAxis: number;
  plotWidth: number;
  plotHeight: number;
  contentWidth: number;
  contentHeight: number;
}

export interface TimeTick {
  value: number;
  expanded: boolean;
}

export function getChartMetrics(domain: { min: number; max: number }, viewportSize: { width: number; height: number }): ChartMetrics {
  const leftAxis = 64;
  const topAxis = 48;
  const rightPadding = 48;
  const bottomPadding = 40;
  const minPlotWidth = 1320;
  const minPlotHeight = Math.max(720, Math.ceil(domain.max * 56));
  const plotWidth = Math.max(minPlotWidth, viewportSize.width - leftAxis - rightPadding);
  const plotHeight = Math.max(minPlotHeight, viewportSize.height - topAxis - bottomPadding);

  return {
    leftAxis,
    topAxis,
    plotWidth,
    plotHeight,
    contentWidth: leftAxis + plotWidth + rightPadding,
    contentHeight: topAxis + plotHeight + bottomPadding,
  };
}

export function getAdaptiveTimeScale(
  levelSession: ActivityLevelSession,
  plottedGroups: PlottedSegmentGroup[],
  domain: { min: number; max: number },
): AdaptiveTimeScale {
  const expandedRanges = mergeTimeExpansionRanges(
    plottedGroups
      .filter(({ runs }) => runs.length > 8)
      .map(({ group, runs }) => {
        const start = getElapsedMinutes(group.FirstStartedAtUtc, levelSession.OpenedAtUtc);
        const lastStarted = getElapsedMinutes(group.LastStartedAtUtc, levelSession.OpenedAtUtc);
        const end = Math.min(domain.max, Math.max(start + 1.5, lastStarted + 1));
        const extra = Math.max(0, (runs.length - 6) * 1.15);

        return { start, end, extra };
      }),
  );

  const expandedMax = domain.max + expandedRanges.reduce((total, range) => total + range.extra, 0);
  const toPosition = (value: number) => {
    const clamped = Math.max(domain.min, Math.min(domain.max, value));

    return expandedRanges.reduce((position, range) => {
      if (clamped <= range.start) return position;
      if (clamped >= range.end) return position + range.extra;

      const progress = (clamped - range.start) / (range.end - range.start);
      return position + range.extra * progress;
    }, clamped);
  };

  return {
    domain: {
      min: domain.min,
      max: expandedMax,
    },
    expandedRanges,
    toPosition,
    toPercent: (value: number) => {
      if (expandedMax <= domain.min) return 0;
      return Math.max(0, Math.min(100, ((toPosition(value) - domain.min) / (expandedMax - domain.min)) * 100));
    },
  };
}

function mergeTimeExpansionRanges(ranges: TimeExpansionRange[]) {
  const sortedRanges = [...ranges]
    .filter((range) => range.end > range.start && range.extra > 0)
    .sort((left, right) => left.start - right.start);
  const mergedRanges: TimeExpansionRange[] = [];

  for (const range of sortedRanges) {
    const previous = mergedRanges.at(-1);
    if (!previous || range.start > previous.end) {
      mergedRanges.push({ ...range });
      continue;
    }

    previous.end = Math.max(previous.end, range.end);
    previous.extra += range.extra;
  }

  return mergedRanges;
}

export function updateScrollState(
  element: HTMLDivElement | null,
  setScrollState: Dispatch<
    SetStateAction<{
      canScrollLeft: boolean;
      canScrollRight: boolean;
      canScrollUp: boolean;
      canScrollDown: boolean;
    }>
  >,
) {
  if (!element) return;

  const maxScrollLeft = element.scrollWidth - element.clientWidth;
  const maxScrollTop = element.scrollHeight - element.clientHeight;

  setScrollState({
    canScrollLeft: element.scrollLeft > 1,
    canScrollRight: element.scrollLeft < maxScrollLeft - 1,
    canScrollUp: element.scrollTop > 1,
    canScrollDown: element.scrollTop < maxScrollTop - 1,
  });
}

export function getTimeTicks(domain: { min: number; max: number }, timeScale: AdaptiveTimeScale): TimeTick[] {
  const ticks = new Map<string, { value: number; expanded: boolean }>();

  for (let tick = domain.min; tick <= domain.max; tick += 2) {
    ticks.set(tick.toFixed(3), { value: tick, expanded: false });
  }

  for (const range of timeScale.expandedRanges) {
    const firstTick = Math.ceil(range.start * 2) / 2;
    for (let tick = firstTick; tick <= range.end; tick += 0.5) {
      const roundedTick = Math.round(tick * 2) / 2;
      ticks.set(roundedTick.toFixed(3), { value: roundedTick, expanded: true });
    }
  }

  return Array.from(ticks.values()).sort((left, right) => left.value - right.value);
}

export function getTileTicks() {
  return Array.from({ length: 11 }, (_, index) => index * 10);
}

export function getExpandedTileTicks() {
  return Array.from({ length: 20 }, (_, index) => (index + 1) * 5).filter((percent) => percent % 10 !== 0);
}

export function getElapsedMinutes(value: string | null | undefined, origin: string) {
  const current = value ? new Date(value).getTime() : Number.NaN;
  const start = new Date(origin).getTime();
  if (Number.isNaN(current) || Number.isNaN(start)) return 0;
  return Math.max(0, (current - start) / 60000);
}

export function tileToPercent(tile: number, levelTileCount: number) {
  if (levelTileCount <= 0) return 0;
  return Math.max(0, Math.min(100, (tile / levelTileCount) * 100));
}

export function getGroupWidthPercent(group: ActivitySegmentGroup, levelTileCount: number) {
  const start = tileToPercent(group.StartTile, levelTileCount);
  const end = tileToPercent(group.BestLastTile, levelTileCount);
  return Math.max(8, Math.min(100 - start, end - start));
}

export function getRunLeftWithinGroup(run: ActivityRun, group: ActivitySegmentGroup, levelTileCount: number) {
  const groupWidth = getGroupWidthPercent(group, levelTileCount);
  if (groupWidth <= 0) return 0;
  const runLeft = tileToPercent(run.StartTile, levelTileCount);
  const groupLeft = tileToPercent(group.StartTile, levelTileCount);
  return Math.max(0, Math.min(100, ((runLeft - groupLeft) / groupWidth) * 100));
}

export function getRunWidthWithinGroup(run: ActivityRun, group: ActivitySegmentGroup, levelTileCount: number) {
  const groupWidth = getGroupWidthPercent(group, levelTileCount);
  if (groupWidth <= 0) return 100;
  const runStart = tileToPercent(run.StartTile, levelTileCount);
  const runEnd = tileToPercent(run.LastTile ?? run.StartTile, levelTileCount);
  return Math.max(8, Math.min(100, ((runEnd - runStart) / groupWidth) * 100));
}

export function getTooltipPosition(event: MouseEvent<HTMLElement>) {
  const width = 256;
  const height = 184;
  const offset = 14;
  const left = event.clientX + width + offset > window.innerWidth ? event.clientX - width - offset : event.clientX + offset;
  const top = event.clientY + height + offset > window.innerHeight ? event.clientY - height - offset : event.clientY + offset;

  return {
    left: Math.max(8, left),
    top: Math.max(8, top),
  };
}

export function formatElapsedMinute(value: number) {
  if (value === 0) return "0";
  const roundedSeconds = Math.round(value * 60);
  const minutes = Math.floor(roundedSeconds / 60);
  const seconds = roundedSeconds % 60;
  if (seconds === 0) return `${minutes}m`;
  return `${minutes}:${String(seconds).padStart(2, "0")}`;
}
