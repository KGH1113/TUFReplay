import { Alert02Icon, Menu01Icon, RefreshIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import {
  type ReactNode,
  type RefObject,
  useCallback,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { AttemptsTable } from "@/components/activity/attempts-table";
import { EmbeddedChart, type EmbeddedChartHandle } from "@/components/activity/embedded-chart";
import { LevelHeader } from "@/components/activity/level-header";
import { RunDeleteDialog } from "@/components/activity/run-delete-dialog";
import type { RunMenuHandlers } from "@/components/activity/run-menu";
import { RunRecordingsDialog } from "@/components/activity/run-recordings-dialog";
import { useRunDeleteRequest } from "@/hooks/activity/use-run-delete-request";
import { usePersistedLayout } from "@/hooks/shared/use-persisted-layout";
import { useStableCallback } from "@/hooks/shared/use-stable-callback";
import type { LevelRunSummary } from "@/models/activity/activity-data";
import type {
  ActivityChart,
  ActivityRun,
  LevelCard,
  LevelMetadata,
  RunMarker,
} from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import type { RunRecordingActions } from "@/models/activity/run-recordings";
import type { ReplayStatus } from "@/models/replay/replay-model";
import { type RunReplayContext, runReplayActive } from "@/models/replay/run-replay-presentation";
import { Button } from "@/shared/ui/button";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@/shared/ui/empty";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/shared/ui/resizable";

/** The selected level: chart and run details on top, every attempt underneath. */
export function ActivityWorkspace({
  level,
  metadata,
  summary,
  chart,
  runs,
  markers,
  selectedMarker,
  selectedRun,
  loading,
  error,
  chartError,
  readOnly,
  timeZone,
  replayStatus,
  replayPendingRunId,
  replayError,
  replayErrorRunId,
  recordingActions,
  onRetry,
  onOpenSidebar,
  onSelectMarker,
  onToggleRun,
  onFocusRun,
  onPlayReplay,
  onRender,
  onDeleteRun,
  onSetRunLocked,
}: {
  level: LevelCard;
  metadata: LevelMetadata;
  summary: LevelRunSummary;
  chart: ActivityChart | null;
  runs: ActivityRun[];
  markers: RunMarker[];
  selectedMarker: RunMarker | null;
  selectedRun: ActivityRun | null;
  loading: boolean;
  error: string;
  chartError: string;
  readOnly: boolean;
  timeZone: string;
  replayStatus: ReplayStatus;
  replayPendingRunId: string | null;
  replayError: string;
  replayErrorRunId: string | null;
  recordingActions: RunRecordingActions;
  onRetry: () => Promise<void>;
  onOpenSidebar: () => void;
  onSelectMarker: (marker: RunMarker | null) => void;
  onToggleRun: (run: ActivityRun) => void;
  onFocusRun: (run: ActivityRun | null) => void;
  onPlayReplay: (run: ActivityRun) => void;
  onRender?: (run: ActivityRun, trigger?: HTMLElement) => void;
  onDeleteRun: (run: ActivityRun) => Promise<void>;
  onSetRunLocked: (run: ActivityRun, locked: boolean) => Promise<void>;
}) {
  const { t } = useTranslation("activity");
  const { t: replayT } = useTranslation("replay");
  const chartRef = useRef<EmbeddedChartHandle>(null);
  const [recordingsRunId, setRecordingsRunId] = useState<string | null>(null);
  const recordingsRun = runs.find((run) => run.id === recordingsRunId) ?? null;
  const layout = usePersistedLayout("tufreplay.workspace.split");
  const deleteRequest = useRunDeleteRequest(onDeleteRun, replayT("run.deleteFailed"));
  const selectedMarkerId = selectedMarker?.id ?? null;

  const replayContext = useMemo<RunReplayContext>(
    () => ({
      status: replayStatus,
      pendingRunId: replayPendingRunId,
      error: replayError,
      errorRunId: replayErrorRunId,
      readOnly,
    }),
    [readOnly, replayError, replayErrorRunId, replayPendingRunId, replayStatus],
  );

  const play = useStableCallback(onPlayReplay);
  const render = useStableCallback((run: ActivityRun) =>
    onRender?.(
      run,
      document.activeElement instanceof HTMLElement ? document.activeElement : undefined,
    ),
  );
  const requestDelete = useStableCallback(deleteRequest.request);
  const setLocked = useStableCallback(onSetRunLocked);
  const hasRender = Boolean(onRender);
  const showRecordings = useCallback((run: ActivityRun) => setRecordingsRunId(run.id), []);
  const toggleLock = useCallback(
    (run: ActivityRun) => {
      void setLocked(run, !run.locked).catch((cause: unknown) => {
        toast.error(localizedErrorMessage(cause, t("lock.failed")));
      });
    },
    [setLocked, t],
  );
  const menuHandlers = useMemo(
    (): RunMenuHandlers => ({
      onPlay: play,
      onRender: hasRender ? render : undefined,
      onShowRecordings: showRecordings,
      onToggleLock: toggleLock,
      onDelete: requestDelete,
    }),
    [hasRender, play, render, requestDelete, showRecordings, toggleLock],
  );
  const selectFloor = useCallback(
    (floor: number) => {
      const marker = markers.find((candidate) => candidate.floorIndex === floor);
      // Floors without attempts keep the current filter; only markers change it.
      if (marker) onSelectMarker(marker);
    },
    [markers, onSelectMarker],
  );
  const selectMarkerById = useCallback(
    (id: string) => onSelectMarker(markers.find((marker) => marker.id === id) ?? null),
    [markers, onSelectMarker],
  );

  useLayoutEffect(() => {
    if (!selectedMarkerId) return;
    const frame = requestAnimationFrame(() => chartRef.current?.refocusSelection());
    return () => cancelAnimationFrame(frame);
  }, [selectedMarkerId]);

  const refreshBanner =
    error && runs.length > 0 ? (
      <div
        role="alert"
        className="mx-4 mb-2 flex items-center gap-3 rounded-xl border border-warning/25 bg-warning/8 px-3 py-2 text-xs"
      >
        <HugeiconsIcon
          aria-hidden="true"
          icon={Alert02Icon}
          size={15}
          strokeWidth={2}
          className="shrink-0 text-warning"
        />
        <p className="min-w-0 flex-1 leading-relaxed">
          <span className="font-semibold">{t("chart.refreshError")}</span>{" "}
          <span className="text-muted-foreground">
            {error} {t("chart.showingPreviousRuns")}
          </span>
        </p>
        <Button size="xs" variant="secondary" onClick={() => void onRetry()}>
          {t("chart.retry")}
        </Button>
      </div>
    ) : null;

  return (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col">
      <LevelHeader
        level={level}
        metadata={metadata}
        summary={summary}
        timeZone={timeZone}
        leading={
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            className="-ml-1.5 shrink-0 lg:hidden"
            aria-label={t("sidebar.open")}
            onClick={onOpenSidebar}
          >
            <HugeiconsIcon aria-hidden="true" icon={Menu01Icon} className="size-5" />
          </Button>
        }
      />
      {error && runs.length === 0 && !loading ? (
        <WorkspaceState
          title={t("chart.loadError")}
          body={error}
          action={
            <Button size="sm" variant="secondary" onClick={() => void onRetry()}>
              <HugeiconsIcon aria-hidden="true" icon={RefreshIcon} className="size-4" />
              {t("chart.retry")}
            </Button>
          }
        />
      ) : (
        <ResizablePanelGroup
          orientation="vertical"
          defaultLayout={layout.defaultLayout}
          onLayoutChanged={layout.onLayoutChanged}
          className="min-h-0 flex-1"
        >
          <ResizablePanel id="chart" defaultSize="50" minSize="22">
            <div className="relative flex h-full min-h-0 px-4 pb-2">
              <ChartArea
                chartRef={chartRef}
                chart={chart}
                chartAvailable={level.chartAvailable}
                loading={loading}
                chartError={chartError}
                markers={markers}
                selectedMarker={selectedMarker}
                selectedRun={selectedRun}
                onMarkerSelect={selectMarkerById}
                onFloorSelect={selectFloor}
              />
            </div>
          </ResizablePanel>
          <ResizableHandle
            withHandle
            aria-label={t("workspace.resize")}
            className="mx-4 bg-transparent transition-colors hover:bg-border focus-visible:bg-primary/50 [&>div]:h-1 [&>div]:w-10 [&>div]:rotate-0! [&>div]:bg-foreground/15 [&>div]:transition-colors hover:[&>div]:bg-foreground/35"
          />
          <ResizablePanel id="attempts" defaultSize="50" minSize="22">
            <AttemptsTable
              runs={runs}
              scopeKey={level.id}
              loading={loading}
              markers={markers}
              floorCount={chart?.floorCount ?? level.floorCount}
              selectedMarker={selectedMarker}
              selectedRun={selectedRun}
              timeZone={timeZone}
              replayContext={replayContext}
              menuHandlers={menuHandlers}
              banner={refreshBanner}
              onSelectMarker={onSelectMarker}
              onToggleRun={onToggleRun}
              onFocusRun={onFocusRun}
              onSetLocked={onSetRunLocked}
            />
          </ResizablePanel>
        </ResizablePanelGroup>
      )}
      <RunDeleteDialog request={deleteRequest} />
      <RunRecordingsDialog
        run={recordingsRun}
        open={recordingsRun !== null}
        readOnly={readOnly}
        replayActive={recordingsRun ? runReplayActive(recordingsRun, replayContext) : false}
        timeZone={timeZone}
        actions={recordingActions}
        onOpenChange={(open) => {
          if (!open) setRecordingsRunId(null);
        }}
      />
    </div>
  );
}

function ChartArea({
  chartRef,
  chart,
  chartAvailable,
  loading,
  chartError,
  markers,
  selectedMarker,
  selectedRun,
  onMarkerSelect,
  onFloorSelect,
}: {
  chartRef: RefObject<EmbeddedChartHandle | null>;
  chart: ActivityChart | null;
  chartAvailable: boolean;
  loading: boolean;
  chartError: string;
  markers: RunMarker[];
  selectedMarker: RunMarker | null;
  selectedRun: ActivityRun | null;
  onMarkerSelect: (id: string) => void;
  onFloorSelect: (floor: number) => void;
}) {
  const { t } = useTranslation("activity");
  if (!chartAvailable)
    return <ChartState title={t("chart.unavailable")} body={t("chart.missingStoredChart")} />;
  if (!chart)
    return (
      <ChartState
        title={loading ? t("chart.loading") : t("chart.unavailable")}
        body={loading ? t("chart.loadingIncrementally") : chartError || t("chart.noData")}
        pending={loading}
      />
    );
  return (
    <div className="relative flex min-w-0 flex-1">
      <EmbeddedChart
        ref={chartRef}
        chart={chart}
        markers={markers}
        selectedMarker={selectedMarker}
        selectedRun={selectedRun}
        onMarkerSelect={onMarkerSelect}
        onFloorSelect={onFloorSelect}
      />
      {markers.length > 1 && !selectedMarker && !selectedRun ? (
        <p className="material-chrome pointer-events-none absolute bottom-3 left-3 rounded-full px-3 py-1.5 text-[11px] font-medium text-foreground/80">
          {t("chart.markerHint")}
        </p>
      ) : null}
    </div>
  );
}

function ChartState({
  title,
  body,
  pending = false,
}: {
  title: string;
  body: string;
  pending?: boolean;
}) {
  return (
    <div className="panel grid min-w-0 flex-1 place-items-center">
      <Empty className="p-8">
        <EmptyHeader>
          <EmptyTitle className={pending ? "animate-pulse motion-reduce:animate-none" : undefined}>
            {title}
          </EmptyTitle>
          <EmptyDescription>{body}</EmptyDescription>
        </EmptyHeader>
      </Empty>
    </div>
  );
}

function WorkspaceState({
  title,
  body,
  action,
}: {
  title: string;
  body: string;
  action?: ReactNode;
}) {
  return (
    <div className="grid min-h-0 flex-1 place-items-center px-4 pb-4">
      <Empty>
        <EmptyHeader>
          <EmptyTitle>{title}</EmptyTitle>
          <EmptyDescription>{body}</EmptyDescription>
        </EmptyHeader>
        {action}
      </Empty>
    </div>
  );
}
