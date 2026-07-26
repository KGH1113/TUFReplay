import {
  ArrowDown02Icon,
  ArrowUp02Icon,
  ChartAverageIcon,
  Clock01Icon,
  DashboardSpeed01Icon,
  Loading03Icon,
  PercentIcon,
  PlayIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "../../ui/tooltip.component";
import { cn } from "../../ui/ui-class.utils";
import type { ActivityChart, ActivityRun, ReplayStatus, RunMarker } from "../activity.model";
import { EmbeddedChart, type EmbeddedChartHandle } from "../chart/embedded-chart.component";
import { runsForMarker } from "../lib/activity-data.utils";
import { formatTimeWithOffsetParts } from "../lib/activity-date.utils";
import { formatXAccuracy } from "../lib/x-accuracy.format";
import { RunActionsMenu } from "./run-actions-menu.component";
import { RunDifficultyIcon } from "./run-difficulty-icon.component";
import { RunJudgmentStrip } from "./run-judgment-strip.component";
import { RunNoFailIcon } from "./run-no-fail-icon.component";

type RunSortKey = "progress" | "time" | "pitch" | "accuracy";
type SortDirection = "asc" | "desc";
type PendingRunSortLayout = {
  positions: Map<string, DOMRect>;
  scrollTop: number;
};

const runSortOptions: { value: RunSortKey; label: string }[] = [
  { value: "progress", label: "Progress" },
  { value: "time", label: "Time" },
  { value: "pitch", label: "Pitch" },
  { value: "accuracy", label: "Accuracy" },
];

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
  onExportRun,
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
  onExportRun: (run: ActivityRun) => Promise<void>;
  onDeleteRun: (run: ActivityRun) => Promise<void>;
  onDeleteMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onKeepMicrophoneRecording: (run: ActivityRun) => Promise<void>;
}) {
  const chartRef = useRef<EmbeddedChartHandle>(null);
  const runListRef = useRef<HTMLDivElement>(null);
  const pendingRunSortLayoutRef = useRef<PendingRunSortLayout>(null);
  const [runSort, setRunSort] = useState<RunSortKey>("time");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");
  const captureRunSortLayout = useCallback(() => {
    const runList = runListRef.current;
    if (!runList) return;

    const positions = new Map<string, DOMRect>();
    for (const element of runList.querySelectorAll<HTMLElement>("[data-run-id]")) {
      const runId = element.dataset.runId;
      if (!runId) continue;
      for (const animation of element.getAnimations()) {
        if (animation.id === "run-sort") animation.cancel();
      }
      positions.set(runId, element.getBoundingClientRect());
    }

    pendingRunSortLayoutRef.current = {
      positions,
      scrollTop: runList.parentElement?.scrollTop ?? 0,
    };
  }, []);
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
  const selectedRuns = runsForMarker(runs, selectedMarker);
  const sortedRuns = sortRuns(selectedRuns, runSort, sortDirection);
  useLayoutEffect(() => {
    void runSort;
    void sortDirection;

    const pendingLayout = pendingRunSortLayoutRef.current;
    pendingRunSortLayoutRef.current = null;
    const runList = runListRef.current;
    if (!pendingLayout || !runList) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const scrollDelta = (runList.parentElement?.scrollTop ?? 0) - pendingLayout.scrollTop;
    for (const element of runList.querySelectorAll<HTMLElement>("[data-run-id]")) {
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
  }, [runSort, sortDirection]);
  if (error) return <StatePanel title="Could not load activity" body={error} />;
  if (!chartAvailable)
    return (
      <StatePanel
        title="Chart unavailable"
        body="This session has no stored chart text. Its level remains listed and browsable."
      />
    );
  if (!chart)
    return (
      <StatePanel
        title={loading ? "Loading session…" : "Chart unavailable"}
        body={
          loading
            ? "Runs and chart pages are loading incrementally."
            : "No chart data was returned."
        }
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
        onTransitionEnd={(event) => {
          if (
            event.currentTarget !== event.target ||
            event.propertyName !== "width" ||
            !selectedMarker
          )
            return;
          chartRef.current?.refocusSelection();
        }}
        className={cn(
          "flex min-h-0 shrink-0 flex-col overflow-hidden border-l bg-muted/10 transition-[width,padding,border-color] duration-300 ease-[cubic-bezier(0.22,1,0.36,1)] motion-reduce:transition-none",
          selectedMarker
            ? "w-96 border-border p-3"
            : "pointer-events-none w-0 border-transparent p-0",
        )}
      >
        {selectedMarker ? (
          <div className="flex min-h-0 min-w-[22.5rem] flex-1 flex-col">
            <div className="mb-3 shrink-0">
              <div className="mb-1.5 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                Sort by
              </div>
              <fieldset
                className="flex min-w-0 items-center gap-2"
                aria-label="Run sorting controls"
              >
                <div className="grid min-w-0 flex-1 grid-cols-4 rounded-md border border-border bg-background/70 p-1">
                  {runSortOptions.map((option) => (
                    <button
                      key={option.value}
                      type="button"
                      aria-pressed={runSort === option.value}
                      onClick={() => changeRunSort(option.value)}
                      className={cn(
                        "h-7 rounded-sm px-1.5 text-[11px] font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                        runSort === option.value && "bg-muted text-foreground shadow-sm",
                      )}
                    >
                      {option.label}
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
            <div className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto overscroll-contain pr-1">
              <TooltipProvider>
                <div ref={runListRef} className="space-y-2">
                  {sortedRuns.map((run) => {
                    const active = selectedRun?.Id === run.Id;
                    return (
                      <RunCard
                        key={run.Id}
                        run={run}
                        active={active}
                        readOnly={readOnly}
                        timeZone={timeZone}
                        replayStatus={replayStatus}
                        replayPendingRunId={replayPendingRunId}
                        replayError={replayError}
                        replayErrorRunId={replayErrorRunId}
                        onSelect={() => onSelectRun(run)}
                        onPlayReplay={onPlayReplay}
                        onExportRun={onExportRun}
                        onDeleteRun={onDeleteRun}
                        onKeepMicrophoneRecording={onKeepMicrophoneRecording}
                        onDeleteMicrophoneRecording={onDeleteMicrophoneRecording}
                      />
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

function RunCard({
  run,
  active,
  readOnly,
  timeZone,
  replayStatus,
  replayPendingRunId,
  replayError,
  replayErrorRunId,
  onSelect,
  onPlayReplay,
  onExportRun,
  onDeleteRun,
  onKeepMicrophoneRecording,
  onDeleteMicrophoneRecording,
}: {
  run: ActivityRun;
  active: boolean;
  readOnly: boolean;
  timeZone: string;
  replayStatus: ReplayStatus;
  replayPendingRunId: string | null;
  replayError: string;
  replayErrorRunId: string | null;
  onSelect: () => void;
  onPlayReplay: (run: ActivityRun) => void;
  onExportRun: (run: ActivityRun) => Promise<void>;
  onDeleteRun: (run: ActivityRun) => Promise<void>;
  onKeepMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onDeleteMicrophoneRecording: (run: ActivityRun) => Promise<void>;
}) {
  const statusMatches = replayStatus.RunId === run.Id;
  const runDeleteDisabled =
    replayPendingRunId === run.Id ||
    (statusMatches &&
      (replayStatus.State === "preparing" ||
        replayStatus.State === "opening_level" ||
        replayStatus.State === "waiting_for_focus" ||
        replayStatus.State === "starting" ||
        replayStatus.State === "playing" ||
        replayStatus.State === "returning_to_editor"));

  return (
    <div
      data-run-id={run.Id}
      className={cn(
        "group/run relative rounded-md border border-border bg-background/60 text-xs transition-colors hover:border-primary/60",
      )}
    >
      <div className="flex min-h-8 items-center gap-2 px-3 pt-2.5">
        <RunReplayButton
          run={run}
          status={replayStatus}
          pendingRunId={replayPendingRunId}
          error={replayError}
          errorRunId={replayErrorRunId}
          onPlay={onPlayReplay}
          disabled={readOnly}
        />
        <div className="ml-auto flex h-8 items-center gap-1">
          <RunDifficultyIcon difficulty={run.JudgmentDifficulty} />
          <RunNoFailIcon enabled={run.NoFailMode} />
          {run.HasMicrophoneRecording ? <MicrophoneRecordingIndicator /> : null}
        </div>
        <RunActionsMenu
          run={run}
          disabled={readOnly}
          runDeleteDisabled={runDeleteDisabled}
          onExportRun={onExportRun}
          onKeepMicrophoneRecording={onKeepMicrophoneRecording}
          onDeleteMicrophoneRecording={onDeleteMicrophoneRecording}
          onDeleteRun={onDeleteRun}
        />
      </div>

      <button
        type="button"
        aria-label={`Select run ${run.RunIndex}`}
        aria-pressed={active}
        onClick={onSelect}
        className="block w-full rounded-b-md p-3 pt-2 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
      >
        <RunCardContent run={run} timeZone={timeZone} />
        <RunJudgmentStrip counts={run.JudgmentCounts} />
      </button>
    </div>
  );
}

function MicrophoneRecordingIndicator() {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          role="img"
          aria-label="Microphone recording available"
          className="-ml-1 grid size-6 shrink-0 place-items-center text-white"
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 24 24"
            className="size-4 -translate-y-[0.5px] overflow-visible"
            fill="none"
          >
            <rect x="8" y="2" width="8" height="13" rx="4" fill="currentColor" />
            <path
              d="M5.5 10.75v.5a6.5 6.5 0 0 0 13 0v-.5M12 17.75V22M9 22h6"
              stroke="currentColor"
              strokeWidth="2.25"
              strokeLinecap="round"
            />
          </svg>
        </span>
      </TooltipTrigger>
      <TooltipContent side="top">Microphone recording available</TooltipContent>
    </Tooltip>
  );
}

function RunCardContent({ run, timeZone }: { run: ActivityRun; timeZone: string }) {
  const startedAt = formatTimeWithOffsetParts(run.StartedAtUtc, timeZone);

  return (
    <div className="grid grid-cols-2 gap-x-5 gap-y-2 rounded-md bg-muted/25 p-2.5">
      <ScoreboardMetric
        icon={PercentIcon}
        label="Progress"
        value={`${runStartProgressPercent(run)}% → ${runProgressPercent(run)}%`}
      />
      <ScoreboardMetric
        icon={DashboardSpeed01Icon}
        label="Pitch"
        value={`${run.LevelPitchPercent ?? "?"}%`}
      />
      <ScoreboardMetric
        icon={ChartAverageIcon}
        label="X-Accuracy"
        value={formatXAccuracy(run.XAccuracy)}
      />
      <ScoreboardMetric
        icon={Clock01Icon}
        label="Started at"
        value={startedAt.time}
        suffix={startedAt.offset ? `(${startedAt.offset})` : undefined}
      />
    </div>
  );
}

function ScoreboardMetric({
  icon,
  label,
  value,
  suffix,
}: {
  icon: typeof PercentIcon;
  label: string;
  value: string;
  suffix?: string;
}) {
  return (
    <span
      className="flex min-w-0 items-center gap-2.5"
      title={`${label}: ${value}${suffix ? ` ${suffix}` : ""}`}
    >
      <span className="grid size-7 shrink-0 place-items-center rounded-sm bg-primary/10 text-primary">
        <HugeiconsIcon aria-hidden="true" icon={icon} size={15} strokeWidth={2} />
      </span>
      <span className="min-w-0">
        <span className="block text-[8px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">
          {label}
        </span>
        <span className="flex min-w-0 items-baseline gap-1 font-heading font-semibold leading-tight tabular-nums text-foreground">
          <span className="min-w-0 truncate text-[13px]">{value}</span>
          {suffix ? (
            <span className="shrink-0 text-[9px] font-medium text-muted-foreground">{suffix}</span>
          ) : null}
        </span>
      </span>
    </span>
  );
}

function RunReplayButton({
  run,
  status,
  pendingRunId,
  error,
  errorRunId,
  onPlay,
  disabled,
}: {
  run: ActivityRun;
  status: ReplayStatus;
  pendingRunId: string | null;
  error: string;
  errorRunId: string | null;
  onPlay: (run: ActivityRun) => void;
  disabled: boolean;
}) {
  const message = describeReplay(run.Id, status, pendingRunId, error, errorRunId);
  const statusMatches = status.RunId === run.Id;
  const starting =
    pendingRunId === run.Id ||
    (statusMatches &&
      (status.State === "preparing" ||
        status.State === "opening_level" ||
        status.State === "waiting_for_focus" ||
        status.State === "starting" ||
        status.State === "returning_to_editor"));
  const playing = statusMatches && status.State === "playing";
  const failed =
    (errorRunId === run.Id && Boolean(error)) || (statusMatches && status.State === "error");
  const failureKey = failed ? error || status.Message || status.ErrorCode || "replay-error" : "";
  const [failureVisible, setFailureVisible] = useState(false);
  useEffect(() => {
    if (!failureKey) {
      setFailureVisible(false);
      return;
    }
    setFailureVisible(true);
    const timeout = setTimeout(() => setFailureVisible(false), 5_000);
    return () => clearTimeout(timeout);
  }, [failureKey]);
  const label = `Play replay for run ${run.RunIndex}${message ? `. ${message}` : ""}`;

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type="button"
          aria-label={label}
          aria-disabled={disabled || pendingRunId !== null}
          disabled={disabled}
          onClick={() => !disabled && pendingRunId === null && onPlay(run)}
          className={cn(
            "inline-flex h-8 items-center justify-center gap-1.5 rounded-md border border-primary/40 bg-primary/15 px-2 font-heading text-[11px] font-semibold tracking-wide text-primary shadow-[inset_0_1px_0_rgb(255_255_255/0.05)] transition-[color,background-color,border-color,box-shadow,transform,opacity] hover:border-primary/70 hover:bg-primary/25 hover:shadow-sm hover:shadow-primary/10 active:translate-y-px focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background aria-disabled:cursor-wait aria-disabled:opacity-45",
            "min-w-16",
            (starting || playing) && "border-primary/70 bg-primary/25",
            playing && "bg-primary text-primary-foreground shadow-sm shadow-primary/20",
            failureVisible &&
              "border-destructive/50 bg-destructive/10 text-destructive shadow-none hover:border-destructive/70 hover:bg-destructive/15",
          )}
        >
          <HugeiconsIcon
            aria-hidden="true"
            icon={starting ? Loading03Icon : PlayIcon}
            className={cn("size-4", starting && "animate-spin")}
            fill={starting ? "none" : "currentColor"}
            strokeWidth={starting ? 2.2 : 0}
          />
          <span>{playing ? "Playing" : "Play"}</span>
        </button>
      </TooltipTrigger>
      <TooltipContent side="top" align="end">
        {message || "Play replay"}
      </TooltipContent>
    </Tooltip>
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
  const label = direction === "asc" ? "Ascending" : "Descending";
  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={selected}
      title={label}
      onClick={() => onSelect(direction)}
      className={cn(
        "grid size-7 place-items-center rounded-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
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
    if (leftValue === null && rightValue === null) return right.RunIndex - left.RunIndex;
    if (leftValue === null) return 1;
    if (rightValue === null) return -1;
    const difference = leftValue - rightValue;
    return (
      (direction === "asc" ? difference : -difference) ||
      (direction === "asc" ? left.RunIndex - right.RunIndex : right.RunIndex - left.RunIndex)
    );
  });
}

function runSortValue(run: ActivityRun, sort: RunSortKey) {
  if (sort === "progress") return runProgressPercent(run);
  if (sort === "pitch") return run.LevelPitchPercent;
  if (sort === "accuracy") return Number.isFinite(run.XAccuracy) ? run.XAccuracy : null;
  const timestamp = Date.parse(run.StartedAtUtc);
  return Number.isNaN(timestamp) ? null : timestamp;
}

function runProgressPercent(run: ActivityRun) {
  const lastTile = Math.max(run.StartTile, run.LastTile ?? run.StartTile);
  return tileProgressPercent(lastTile, run.FloorCount);
}

function runStartProgressPercent(run: ActivityRun) {
  return tileProgressPercent(run.StartTile, run.FloorCount);
}

function tileProgressPercent(tile: number, floorCount: number) {
  return Math.round(Math.min(1, Math.max(0, tile) / Math.max(1, floorCount)) * 100);
}

function describeReplay(
  runId: string,
  status: ReplayStatus,
  pendingRunId: string | null,
  error: string,
  errorRunId: string | null,
) {
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
  if (
    status.State === "preparing" ||
    status.State === "opening_level" ||
    status.State === "waiting_for_focus" ||
    status.State === "starting" ||
    status.State === "playing" ||
    status.State === "returning_to_editor"
  ) {
    return "Starting this run will replace the active replay.";
  }
  return null;
}
