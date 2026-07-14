import { useCallback, useLayoutEffect, useRef, useState } from "react";
import { ArrowDown02Icon, ArrowLeft02Icon, ArrowRight02Icon, ArrowUp02Icon, Clock01Icon, DashboardSpeed01Icon, PercentIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";

import type { ActivityChart, ActivityRun, ReplayStatus, RunMarker } from "../activity.model";
import { formatTimeWithOffset } from "../lib/activity-date.utils";
import { runsForMarker } from "../lib/activity-data.utils";
import { EmbeddedChart, type EmbeddedChartHandle } from "../chart/embedded-chart.component";
import { cn } from "../../ui/ui-class.utils";
import { Button } from "../../ui/button.component";

type RunSortKey = "progress" | "time" | "pitch";
type SortDirection = "asc" | "desc";

const runSortOptions: { value: RunSortKey; label: string }[] = [
  { value: "progress", label: "Progress" },
  { value: "time", label: "Time" },
  { value: "pitch", label: "Pitch" },
];

export function ActivityWorkspace({ chartAvailable, chart, runs, markers, selectedMarker, selectedRun, loading, error, timeZone, replayStatus, replayPendingRunId, replayError, replayErrorRunId, onSelectMarker, onSelectRun, onPlayReplay }: {
  chartAvailable: boolean; chart: ActivityChart | null; runs: ActivityRun[]; markers: RunMarker[]; selectedMarker: RunMarker | null;
  selectedRun: ActivityRun | null; loading: boolean; error: string; timeZone: string;
  replayStatus: ReplayStatus; replayPendingRunId: string | null; replayError: string; replayErrorRunId: string | null;
  onSelectMarker: (marker: RunMarker | null) => void; onSelectRun: (run: ActivityRun) => void; onPlayReplay: (run: ActivityRun) => void;
}) {
  const chartRef = useRef<EmbeddedChartHandle>(null);
  const runListRef = useRef<HTMLDivElement>(null);
  const previousRunPositionsRef = useRef(new Map<string, DOMRect>());
  const [runSort, setRunSort] = useState<RunSortKey>("time");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const selectId = useCallback((id: string) => onSelectMarker(markers.find((marker) => marker.id === id) ?? null), [markers, onSelectMarker]);
  const selectFloor = useCallback((floor: number) => onSelectMarker(markers.find((marker) => marker.floorIndex === floor) ?? null), [markers, onSelectMarker]);
  const selectedMarkerIndex = selectedMarker ? markers.findIndex((marker) => marker.id === selectedMarker.id) : -1;
  const previousMarker = selectedMarkerIndex > 0 ? markers[selectedMarkerIndex - 1] : null;
  const nextMarker = selectedMarkerIndex >= 0 && selectedMarkerIndex < markers.length - 1 ? markers[selectedMarkerIndex + 1] : null;
  const selectedRuns = runsForMarker(runs, selectedMarker);
  const sortedRuns = sortRuns(selectedRuns, runSort, sortDirection);
  const replayMessage = selectedRun ? describeReplay(selectedRun.Id, replayStatus, replayPendingRunId, replayError, replayErrorRunId) : null;
  useLayoutEffect(() => {
    const elements = runListRef.current?.querySelectorAll<HTMLElement>("[data-run-id]");
    if (!elements) return;
    const nextPositions = new Map<string, DOMRect>();
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    elements.forEach((element) => {
      const runId = element.dataset.runId;
      if (!runId) return;
      const nextPosition = element.getBoundingClientRect();
      nextPositions.set(runId, nextPosition);
      const previousPosition = previousRunPositionsRef.current.get(runId);
      if (!previousPosition || reduceMotion) return;
      const x = previousPosition.left - nextPosition.left;
      const y = previousPosition.top - nextPosition.top;
      if (Math.abs(x) < 0.5 && Math.abs(y) < 0.5) return;
      element.animate(
        [{ transform: `translate(${x}px, ${y}px)` }, { transform: "translate(0, 0)" }],
        { duration: 360, easing: "cubic-bezier(0.22, 1, 0.36, 1)" },
      );
    });
    previousRunPositionsRef.current = nextPositions;
  }, [sortedRuns]);
  if (error) return <StatePanel title="Could not load activity" body={error} />;
  if (!chartAvailable) return <StatePanel title="Chart unavailable" body="This session has no stored chart text. Its level remains listed and browsable." />;
  if (!chart) return <StatePanel title={loading ? "Loading session…" : "Chart unavailable"} body={loading ? "Runs and chart pages are loading incrementally." : "No chart data was returned."} />;
  return <section className="flex min-h-0 flex-1">
    <EmbeddedChart ref={chartRef} chart={chart} markers={markers} selectedMarker={selectedMarker} selectedRun={selectedRun} onMarkerSelect={selectId} onFloorSelect={selectFloor} />
    <aside
      aria-hidden={!selectedMarker}
      onTransitionEnd={(event) => {
        if (event.currentTarget !== event.target || event.propertyName !== "width" || !selectedMarker) return;
        chartRef.current?.refocusSelection();
      }}
      className={cn(
        "flex min-h-0 shrink-0 flex-col overflow-hidden border-l bg-muted/10 transition-[width,padding,border-color] duration-300 ease-[cubic-bezier(0.22,1,0.36,1)] motion-reduce:transition-none",
        selectedMarker ? "w-80 border-border p-3" : "pointer-events-none w-0 border-transparent p-0",
      )}
    >
      {selectedMarker ? <div className="flex min-h-0 min-w-[18.5rem] flex-1 flex-col">
        <div className="mb-3 flex shrink-0 items-center justify-between border-b border-border pb-3">
          <div>
            <div className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Run start</div>
            <div className="mt-0.5 font-heading text-sm font-semibold tabular-nums">Floor {selectedMarker.floorIndex}</div>
          </div>
          <div className="grid grid-cols-2 rounded-md border border-border bg-background/70 p-1" aria-label="Run start navigation">
            <RunStartNavigationButton label="Previous run start" icon={ArrowLeft02Icon} marker={previousMarker} onSelect={onSelectMarker} />
            <RunStartNavigationButton label="Next run start" icon={ArrowRight02Icon} marker={nextMarker} onSelect={onSelectMarker} />
          </div>
        </div>
        <div className="mb-3 shrink-0">
          <div className="mb-1.5 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Sort by</div>
          <div className="flex items-center gap-2" aria-label="Run sorting controls">
            <div className="grid min-w-0 flex-1 grid-cols-3 rounded-md border border-border bg-background/70 p-1">
              {runSortOptions.map((option) => <button key={option.value} type="button" aria-pressed={runSort === option.value} onClick={() => setRunSort(option.value)} className={cn("h-7 rounded-sm px-1.5 text-[11px] font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring", runSort === option.value && "bg-muted text-foreground shadow-sm")}>{option.label}</button>)}
            </div>
            <div className="grid grid-cols-2 rounded-md border border-border bg-background/70 p-1">
              <SortDirectionButton direction="asc" selected={sortDirection === "asc"} onSelect={setSortDirection} />
              <SortDirectionButton direction="desc" selected={sortDirection === "desc"} onSelect={setSortDirection} />
            </div>
          </div>
        </div>
        <div className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto overscroll-contain pr-1"><div ref={runListRef} className="space-y-2">{sortedRuns.map((run) => {
          const active = selectedRun?.Id === run.Id;
          return <button key={run.Id} data-run-id={run.Id} type="button" aria-pressed={active} onClick={() => onSelectRun(run)} className={cn("block w-full rounded-md border border-border bg-background/60 p-3 text-left text-xs transition-colors hover:border-primary/50 hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring", active && "border-primary bg-primary/10 ring-1 ring-primary/40")}>
            <div className="flex items-center justify-between font-medium"><span className="inline-flex min-w-7 items-center justify-center rounded-sm border border-primary/30 bg-primary/10 px-1.5 py-0.5 font-heading text-[11px] font-semibold tabular-nums text-primary">#{run.RunIndex}</span><span>{run.Result}</span></div>
            <div className="mt-2.5 grid grid-cols-2 gap-2">
              <RunMetric icon={PercentIcon} label="Progress" value={`${run.StartTile} → ${runProgressPercent(run)}%`} />
              <RunMetric className="justify-self-end" icon={DashboardSpeed01Icon} label="Pitch" value={`${run.LevelPitchPercent ?? "?"}%`} />
              <RunMetric className="col-span-2" icon={Clock01Icon} label="Started at" value={formatTimeWithOffset(run.StartedAtUtc, timeZone)} />
            </div>
          </button>;
        })}</div></div>
        {selectedRun ? <div className="mt-3 shrink-0 border-t border-border pt-3"><div className="grid grid-cols-2 gap-2"><Button type="button" disabled={replayPendingRunId === selectedRun.Id} onClick={() => onPlayReplay(selectedRun)}>{replayPendingRunId === selectedRun.Id ? "Starting…" : "Play replay"}</Button><Button type="button" variant="outline" onClick={() => chartRef.current?.fitEntireRun()}>Fit entire run</Button></div>{replayMessage ? <p aria-live="polite" className={cn("mt-2 text-xs text-muted-foreground", replayErrorRunId === selectedRun.Id || replayStatus.RunId === selectedRun.Id && replayStatus.State === "error" ? "text-destructive" : null)}>{replayMessage}</p> : null}</div> : null}
      </div> : null}
    </aside>
  </section>;
}

function StatePanel({ title, body }: { title: string; body: string }) { return <section className="grid min-h-0 flex-1 place-items-center p-8 text-center"><div><h2 className="font-heading text-xl font-semibold">{title}</h2><p className="mt-2 text-sm text-muted-foreground">{body}</p></div></section>; }

function RunMetric({ icon, label, value, className }: { icon: typeof PercentIcon; label: string; value: string; className?: string }) {
  return <span className={cn("inline-flex w-fit min-w-0 items-center gap-2 whitespace-nowrap", className)} title={label}>
    <span className="grid size-6 shrink-0 place-items-center rounded-sm bg-muted/70 text-muted-foreground">
      <HugeiconsIcon aria-hidden="true" icon={icon} size={14} strokeWidth={2} />
    </span>
    <span className="font-medium tabular-nums text-foreground/85">{value}</span>
  </span>;
}

function SortDirectionButton({ direction, selected, onSelect }: { direction: SortDirection; selected: boolean; onSelect: (direction: SortDirection) => void }) {
  const label = direction === "asc" ? "Ascending" : "Descending";
  return <button type="button" aria-label={label} aria-pressed={selected} title={label} onClick={() => onSelect(direction)} className={cn("grid size-7 place-items-center rounded-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring", selected && "bg-primary/15 text-primary shadow-sm")}>
    <HugeiconsIcon aria-hidden="true" icon={direction === "asc" ? ArrowUp02Icon : ArrowDown02Icon} size={15} strokeWidth={2} />
  </button>;
}

function RunStartNavigationButton({ label, icon, marker, onSelect }: { label: string; icon: typeof ArrowLeft02Icon; marker: RunMarker | null; onSelect: (marker: RunMarker | null) => void }) {
  return <button type="button" aria-label={label} title={label} disabled={!marker} onClick={() => marker && onSelect(marker)} className="grid size-8 place-items-center rounded-sm text-foreground transition-colors hover:bg-muted disabled:cursor-not-allowed disabled:text-muted-foreground/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
    <HugeiconsIcon aria-hidden="true" icon={icon} size={16} strokeWidth={2} />
  </button>;
}

function sortRuns(runs: ActivityRun[], sort: RunSortKey, direction: SortDirection) {
  return [...runs].sort((left, right) => {
    const leftValue = runSortValue(left, sort);
    const rightValue = runSortValue(right, sort);
    if (leftValue === null && rightValue === null) return right.RunIndex - left.RunIndex;
    if (leftValue === null) return 1;
    if (rightValue === null) return -1;
    const difference = leftValue - rightValue;
    return (direction === "asc" ? difference : -difference) || (direction === "asc" ? left.RunIndex - right.RunIndex : right.RunIndex - left.RunIndex);
  });
}

function runSortValue(run: ActivityRun, sort: RunSortKey) {
  if (sort === "progress") return runProgressPercent(run);
  if (sort === "pitch") return run.LevelPitchPercent;
  const timestamp = Date.parse(run.StartedAtUtc);
  return Number.isNaN(timestamp) ? null : timestamp;
}

function runProgressPercent(run: ActivityRun) {
  const lastTile = Math.max(run.StartTile, run.LastTile ?? run.StartTile);
  return Math.round(Math.min(1, lastTile / Math.max(1, run.FloorCount)) * 100);
}

function describeReplay(runId: string, status: ReplayStatus, pendingRunId: string | null, error: string, errorRunId: string | null) {
  if (pendingRunId === runId) return "Sending replay to ADOFAI…";
  if (errorRunId === runId && error) return error;
  if (status.RunId === runId) {
    if (status.State === "preparing") return "Preparing replay…";
    if (status.State === "opening_level") return "Opening the recorded level…";
    if (status.State === "waiting_for_focus") return "Waiting for ADOFAI to regain focus…";
    if (status.State === "starting") return "Starting replay…";
    if (status.State === "playing") return "Replay is playing.";
    if (status.State === "returning_to_editor") return "Returning to the editor…";
    if (status.State === "completed") return "Replay completed.";
    if (status.State === "cancelled") return "Replay cancelled.";
    if (status.State === "error") return status.Message || status.ErrorCode || "Replay failed.";
    return null;
  }
  if (status.State === "preparing" || status.State === "opening_level" || status.State === "waiting_for_focus" || status.State === "starting" || status.State === "playing" || status.State === "returning_to_editor") {
    return "Starting this run will replace the active replay.";
  }
  return null;
}
