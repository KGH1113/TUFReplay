import { ArrowDown02Icon, ArrowUp02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { EmbeddedChart, type EmbeddedChartHandle } from "@/components/activity/embedded-chart";
import { RunCard } from "@/components/activity/run-card";
import { useStableCallback } from "@/hooks/shared/use-stable-callback";
import type { ActivityChart, ActivityRun, RunMarker } from "@/models/activity/activity-model";
import {
  calculateVirtualRunRange,
  type VirtualRunRange,
} from "@/models/activity/run-list-virtualization";
import type { ReplayStatus } from "@/models/replay/replay-model";
import { cn } from "@/shared/lib/cn";
import { TooltipProvider } from "@/shared/ui/tooltip";

type RunSortKey = "progress" | "time" | "pitch" | "accuracy";
type SortDirection = "asc" | "desc";
type PendingRunSortLayout = {
  positions: Map<string, DOMRect>;
  scrollTop: number;
};

const runSortOptions: RunSortKey[] = ["progress", "time", "pitch", "accuracy"];
const runRowGap = 8;
const estimatedRunRowStride = 188;
const runRowOverscan = 4;

export function ActivityWorkspace({
  chartAvailable,
  chart,
  runs,
  markers,
  selectedMarker,
  selectedRun,
  loading,
  error,
  readOnly,
  timeZone,
  replayStatus,
  replayPendingRunId,
  replayError,
  replayErrorRunId,
  onSelectMarker,
  onSelectRun,
  onPlayReplay,
  onDeleteRun,
  onDeleteMicrophoneRecording,
  onKeepMicrophoneRecording,
}: {
  chartAvailable: boolean;
  chart: ActivityChart | null;
  runs: ActivityRun[];
  markers: RunMarker[];
  selectedMarker: RunMarker | null;
  selectedRun: ActivityRun | null;
  loading: boolean;
  error: string;
  readOnly: boolean;
  timeZone: string;
  replayStatus: ReplayStatus;
  replayPendingRunId: string | null;
  replayError: string;
  replayErrorRunId: string | null;
  onSelectMarker: (marker: RunMarker | null) => void;
  onSelectRun: (run: ActivityRun) => void;
  onPlayReplay: (run: ActivityRun) => void;
  onDeleteRun: (run: ActivityRun) => Promise<void>;
  onDeleteMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onKeepMicrophoneRecording: (run: ActivityRun) => Promise<void>;
}) {
  const { t } = useTranslation("activity");
  const chartRef = useRef<EmbeddedChartHandle>(null);
  const runScrollRef = useRef<HTMLDivElement>(null);
  const runListRef = useRef<HTMLDivElement>(null);
  const runRowResizeObserverRef = useRef<ResizeObserver | null>(null);
  const pendingRunSortLayoutRef = useRef<PendingRunSortLayout>(null);
  const [runSort, setRunSort] = useState<RunSortKey>("time");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const [runRowStride, setRunRowStride] = useState(estimatedRunRowStride);
  const [visibleRunRange, setVisibleRunRange] = useState<VirtualRunRange>({ start: 0, end: 0 });
  const selectRun = useStableCallback(onSelectRun);
  const playReplay = useStableCallback(onPlayReplay);
  const deleteRun = useStableCallback(onDeleteRun);
  const keepMicrophoneRecording = useStableCallback(onKeepMicrophoneRecording);
  const deleteMicrophoneRecording = useStableCallback(onDeleteMicrophoneRecording);
  const captureRunSortLayout = useCallback(() => {
    const runList = runListRef.current;
    const scroller = runScrollRef.current;
    if (!runList || !scroller) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const positions = new Map<string, DOMRect>();
    for (const element of visibleRunElements(runList, scroller, runRowStride)) {
      const runId = element.dataset.runId;
      if (!runId) continue;
      for (const animation of element.getAnimations()) {
        if (animation.id === "run-sort") animation.cancel();
      }
      positions.set(runId, element.getBoundingClientRect());
    }

    pendingRunSortLayoutRef.current = {
      positions,
      scrollTop: scroller.scrollTop,
    };
  }, [runRowStride]);
  const changeRunSort = useCallback(
    (nextSort: RunSortKey) => {
      if (nextSort === runSort) return;
      captureRunSortLayout();
      setRunSort(nextSort);
    },
    [captureRunSortLayout, runSort],
  );
  const changeSortDirection = useCallback(
    (nextDirection: SortDirection) => {
      if (nextDirection === sortDirection) return;
      captureRunSortLayout();
      setSortDirection(nextDirection);
    },
    [captureRunSortLayout, sortDirection],
  );
  const selectId = useCallback(
    (id: string) => onSelectMarker(markers.find((marker) => marker.id === id) ?? null),
    [markers, onSelectMarker],
  );
  const selectFloor = useCallback(
    (floor: number) =>
      onSelectMarker(markers.find((marker) => marker.floorIndex === floor) ?? null),
    [markers, onSelectMarker],
  );
  const selectedFloor = selectedMarker?.floorIndex ?? null;
  const hasSelectedMarker = selectedMarker !== null;
  const selectedRuns = useMemo(
    () => (selectedFloor === null ? [] : runs.filter((run) => run.startTile === selectedFloor)),
    [runs, selectedFloor],
  );
  const sortedRuns = useMemo(
    () => sortRuns(selectedRuns, runSort, sortDirection),
    [selectedRuns, runSort, sortDirection],
  );
  const renderedRunRange =
    visibleRunRange.end > visibleRunRange.start && visibleRunRange.start < sortedRuns.length
      ? {
          start: visibleRunRange.start,
          end: Math.min(visibleRunRange.end, sortedRuns.length),
        }
      : calculateVirtualRunRange(sortedRuns.length, 0, 0, runRowStride, runRowOverscan);
  const measureRunRow = useCallback((element: HTMLDivElement | null) => {
    runRowResizeObserverRef.current?.disconnect();
    runRowResizeObserverRef.current = null;
    if (!element) return;

    const updateStride = () => {
      const nextStride = element.getBoundingClientRect().height + runRowGap;
      if (nextStride > runRowGap) {
        setRunRowStride((current) => (Math.abs(current - nextStride) < 0.5 ? current : nextStride));
      }
    };
    updateStride();

    const observer = new ResizeObserver(updateStride);
    observer.observe(element);
    runRowResizeObserverRef.current = observer;
  }, []);
  useEffect(() => () => runRowResizeObserverRef.current?.disconnect(), []);
  useLayoutEffect(() => {
    if (!hasSelectedMarker) return;
    const scroller = runScrollRef.current;
    if (!scroller) return;

    let frame = 0;
    const updateRange = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        const next = calculateVirtualRunRange(
          sortedRuns.length,
          scroller.scrollTop,
          scroller.clientHeight,
          runRowStride,
          runRowOverscan,
        );
        setVisibleRunRange((current) =>
          current.start === next.start && current.end === next.end ? current : next,
        );
      });
    };

    updateRange();
    scroller.addEventListener("scroll", updateRange, { passive: true });
    const resizeObserver = new ResizeObserver(updateRange);
    resizeObserver.observe(scroller);
    return () => {
      cancelAnimationFrame(frame);
      scroller.removeEventListener("scroll", updateRange);
      resizeObserver.disconnect();
    };
  }, [hasSelectedMarker, runRowStride, sortedRuns.length]);
  useLayoutEffect(() => {
    void runSort;
    void sortDirection;

    const pendingLayout = pendingRunSortLayoutRef.current;
    pendingRunSortLayoutRef.current = null;
    const runList = runListRef.current;
    const scroller = runScrollRef.current;
    if (!pendingLayout || !runList || !scroller) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const scrollDelta = scroller.scrollTop - pendingLayout.scrollTop;
    for (const element of visibleRunElements(runList, scroller, runRowStride)) {
      const runId = element.dataset.runId;
      if (!runId) continue;
      const previousPosition = pendingLayout.positions.get(runId);
      if (!previousPosition) continue;

      const nextPosition = element.getBoundingClientRect();
      const x = previousPosition.left - nextPosition.left;
      const y = previousPosition.top - scrollDelta - nextPosition.top;
      if (Math.abs(x) < 0.5 && Math.abs(y) < 0.5) continue;

      const animation = element.animate(
        [{ transform: `translate(${x}px, ${y}px)` }, { transform: "translate(0, 0)" }],
        { duration: 360, easing: "cubic-bezier(0.22, 1, 0.36, 1)" },
      );
      animation.id = "run-sort";
    }
  }, [runRowStride, runSort, sortDirection]);
  useLayoutEffect(() => {
    if (!selectedMarker) return;
    const frame = requestAnimationFrame(() => chartRef.current?.refocusSelection());
    return () => cancelAnimationFrame(frame);
  }, [selectedMarker]);
  if (error) return <StatePanel title={t("chart.loadError")} body={error} />;
  if (!chartAvailable)
    return <StatePanel title={t("chart.unavailable")} body={t("chart.missingStoredChart")} />;
  if (!chart)
    return (
      <StatePanel
        title={loading ? t("chart.loading") : t("chart.unavailable")}
        body={loading ? t("chart.loadingIncrementally") : t("chart.noData")}
      />
    );
  return (
    <section className="flex min-h-0 flex-1">
      <EmbeddedChart
        ref={chartRef}
        chart={chart}
        markers={markers}
        selectedMarker={selectedMarker}
        selectedRun={selectedRun}
        onMarkerSelect={selectId}
        onFloorSelect={selectFloor}
      />
      <aside
        aria-hidden={!selectedMarker}
        className={cn(
          "flex min-h-0 shrink-0 flex-col overflow-hidden border-l bg-muted/10",
          selectedMarker
            ? "w-96 border-border p-3"
            : "pointer-events-none w-0 border-transparent p-0",
        )}
      >
        {selectedMarker ? (
          <div className="flex min-h-0 min-w-[22.5rem] flex-1 flex-col motion-safe:animate-in motion-safe:fade-in-0 motion-safe:slide-in-from-right-2 motion-safe:duration-200">
            <div className="mb-3 shrink-0">
              <div className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                {t("run.sortBy")}
              </div>
              <fieldset className="flex min-w-0 items-center gap-2" aria-label={t("sort.label")}>
                <div className="grid min-w-0 flex-1 grid-cols-4 rounded-md border border-border bg-background/70 p-1">
                  {runSortOptions.map((option) => (
                    <button
                      key={option}
                      type="button"
                      aria-pressed={runSort === option}
                      onClick={() => changeRunSort(option)}
                      className={cn(
                        "h-8 rounded-sm px-1.5 text-xs font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                        runSort === option && "bg-muted text-foreground shadow-sm",
                      )}
                    >
                      {t(`sort.${option}`)}
                    </button>
                  ))}
                </div>
                <div className="grid grid-cols-2 rounded-md border border-border bg-background/70 p-1">
                  <SortDirectionButton
                    direction="asc"
                    selected={sortDirection === "asc"}
                    onSelect={changeSortDirection}
                  />
                  <SortDirectionButton
                    direction="desc"
                    selected={sortDirection === "desc"}
                    onSelect={changeSortDirection}
                  />
                </div>
              </fieldset>
            </div>
            <div
              ref={runScrollRef}
              className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto overscroll-contain pr-1"
            >
              <TooltipProvider>
                <div
                  ref={runListRef}
                  data-run-count={sortedRuns.length}
                  className="relative"
                  style={{ height: Math.max(0, sortedRuns.length * runRowStride - runRowGap) }}
                >
                  {sortedRuns
                    .slice(renderedRunRange.start, renderedRunRange.end)
                    .map((run, offset) => {
                      const index = renderedRunRange.start + offset;
                      const active = selectedRun?.id === run.id;
                      return (
                        <div
                          key={run.id}
                          ref={offset === 0 ? measureRunRow : undefined}
                          data-run-index={index}
                          className="absolute inset-x-0"
                          style={{ transform: `translateY(${index * runRowStride}px)` }}
                        >
                          <RunCard
                            run={run}
                            active={active}
                            readOnly={readOnly}
                            timeZone={timeZone}
                            replayStatus={replayStatus}
                            replayPendingRunId={replayPendingRunId}
                            replayError={replayError}
                            replayErrorRunId={replayErrorRunId}
                            onSelect={selectRun}
                            onPlayReplay={playReplay}
                            onDeleteRun={deleteRun}
                            onKeepMicrophoneRecording={keepMicrophoneRecording}
                            onDeleteMicrophoneRecording={deleteMicrophoneRecording}
                          />
                        </div>
                      );
                    })}
                </div>
              </TooltipProvider>
            </div>
          </div>
        ) : null}
      </aside>
    </section>
  );
}

function StatePanel({ title, body }: { title: string; body: string }) {
  return (
    <section className="grid min-h-0 flex-1 place-items-center p-8 text-center">
      <div>
        <h2 className="font-heading text-xl font-semibold">{title}</h2>
        <p className="mt-2 text-sm text-muted-foreground">{body}</p>
      </div>
    </section>
  );
}

function SortDirectionButton({
  direction,
  selected,
  onSelect,
}: {
  direction: SortDirection;
  selected: boolean;
  onSelect: (direction: SortDirection) => void;
}) {
  const { t } = useTranslation("activity");
  const label = t(direction === "asc" ? "run.ascending" : "run.descending");
  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={selected}
      title={label}
      onClick={() => onSelect(direction)}
      className={cn(
        "grid size-8 place-items-center rounded-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
        selected && "bg-primary/15 text-primary shadow-sm",
      )}
    >
      <HugeiconsIcon
        aria-hidden="true"
        icon={direction === "asc" ? ArrowUp02Icon : ArrowDown02Icon}
        size={15}
        strokeWidth={2}
      />
    </button>
  );
}

function sortRuns(runs: ActivityRun[], sort: RunSortKey, direction: SortDirection) {
  return [...runs].sort((left, right) => {
    const leftValue = runSortValue(left, sort);
    const rightValue = runSortValue(right, sort);
    if (leftValue === null && rightValue === null) return right.runIndex - left.runIndex;
    if (leftValue === null) return 1;
    if (rightValue === null) return -1;
    const difference = leftValue - rightValue;
    return (
      (direction === "asc" ? difference : -difference) ||
      (direction === "asc" ? left.runIndex - right.runIndex : right.runIndex - left.runIndex)
    );
  });
}

function visibleRunElements(
  runList: HTMLDivElement,
  scroller: HTMLDivElement,
  runRowStride: number,
): HTMLElement[] {
  const itemCount = Number(runList.dataset.runCount) || 0;
  const range = calculateVirtualRunRange(
    itemCount,
    scroller.scrollTop,
    scroller.clientHeight,
    runRowStride,
    0,
  );
  const elements: HTMLElement[] = [];

  for (const row of runList.querySelectorAll<HTMLElement>("[data-run-index]")) {
    const index = Number(row.dataset.runIndex);
    if (!Number.isInteger(index) || index < range.start || index >= range.end) continue;
    const element = row.querySelector<HTMLElement>("[data-run-id]");
    if (element) elements.push(element);
  }

  return elements;
}

function runSortValue(run: ActivityRun, sort: RunSortKey) {
  if (sort === "progress") return runProgressPercent(run);
  if (sort === "pitch") return run.levelPitchPercent;
  if (sort === "accuracy") return Number.isFinite(run.xAccuracy) ? run.xAccuracy : null;
  const timestamp = Date.parse(run.startedAtUtc);
  return Number.isNaN(timestamp) ? null : timestamp;
}

function runProgressPercent(run: ActivityRun) {
  const lastTile = Math.max(run.startTile, run.lastTile ?? run.startTile);
  return tileProgressPercent(lastTile, run.floorCount);
}

function tileProgressPercent(tile: number, floorCount: number) {
  return Math.round(Math.min(1, Math.max(0, tile) / Math.max(1, floorCount)) * 100);
}
