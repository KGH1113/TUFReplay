import { ArrowDown01Icon, ArrowUp01Icon, UnfoldMoreIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { CSSProperties, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { AttemptFilterMenu } from "@/components/activity/attempt-filter-menu";
import { AttemptRow } from "@/components/activity/attempt-row";
import type { RunMenuHandlers } from "@/components/activity/run-menu";
import { StartTileTimeline } from "@/components/activity/start-tile-timeline";
import {
  type AttemptSortKey,
  type SortDirection,
  useAttemptsTable,
} from "@/hooks/activity/use-attempts-table";
import { useJudgmentTimelinesWaiting } from "@/hooks/activity/use-judgment-timelines-waiting";
import { useStableCallback } from "@/hooks/shared/use-stable-callback";
import { formatTimeWithOffsetParts } from "@/models/activity/activity-date";
import type { ActivityRun, RunMarker } from "@/models/activity/activity-model";
import type { RunReplayContext } from "@/models/replay/run-replay-presentation";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { Skeleton } from "@/shared/ui/skeleton";

export function AttemptsTable({
  runs,
  scopeKey,
  loading,
  markers,
  floorCount,
  selectedMarker,
  selectedRun,
  timeZone,
  replayContext,
  menuHandlers,
  banner,
  onSelectMarker,
  onToggleRun,
  onFocusRun,
  onSetLocked,
}: {
  runs: ActivityRun[];
  /** The level being shown; filters reset when it changes. */
  scopeKey: string;
  loading: boolean;
  markers: RunMarker[];
  floorCount: number;
  selectedMarker: RunMarker | null;
  selectedRun: ActivityRun | null;
  timeZone: string;
  replayContext: RunReplayContext;
  menuHandlers: RunMenuHandlers;
  banner?: ReactNode;
  onSelectMarker: (marker: RunMarker | null) => void;
  onToggleRun: (run: ActivityRun) => void;
  onFocusRun: (run: ActivityRun | null) => void;
  onSetLocked: (run: ActivityRun, locked: boolean) => Promise<void>;
}) {
  const { t, i18n } = useTranslation("activity");
  const toggleRun = useStableCallback(onToggleRun);
  const setLocked = useStableCallback(onSetLocked);
  const table = useAttemptsTable({
    runs,
    scopeKey,
    filterFloor: selectedMarker?.floorIndex ?? null,
    selectedRunId: selectedRun?.id ?? null,
    onSelectRun: onFocusRun,
    onOpenRun: menuHandlers.onPlay,
  });
  const offset = formatTimeWithOffsetParts(
    new Date().toISOString(),
    i18n.resolvedLanguage ?? "en",
    "",
    timeZone,
  ).offset;
  const { sortedRuns, renderedRange, rowStride } = table;
  const rowKeyDown = useStableCallback(table.onKeyDown);
  const timelinesWaiting = useJudgmentTimelinesWaiting();
  const focusableRunId =
    sortedRuns.find((run) => run.id === selectedRun?.id)?.id ?? sortedRuns[0]?.id ?? null;
  const gridStyle = {
    "--judgment-columns": table.judgmentLayout.columns,
    "--judgment-count": table.judgmentLayout.count,
    "--judgment-width": table.judgmentLayout.width,
  } as CSSProperties;

  return (
    <section
      aria-label={t("attempts.title")}
      className="attempts-container flex h-full min-h-0 flex-col"
      style={gridStyle}
    >
      <header className="flex min-h-12 shrink-0 flex-wrap items-center gap-x-4 gap-y-1 px-4 pt-2 pb-2">
        <h2 className="shrink-0 text-sm font-semibold">{t("attempts.title")}</h2>
        {markers.length > 1 || selectedMarker ? (
          <StartTileTimeline
            markers={markers}
            floorCount={floorCount}
            selectedMarker={selectedMarker}
            onSelect={onSelectMarker}
            className="min-w-48 flex-1 @max-[34rem]/attempts:order-last @max-[34rem]/attempts:basis-full"
          />
        ) : (
          <span className="flex-1" />
        )}
        <div className="ml-auto flex shrink-0 items-center gap-0.5">
          <AttemptFilterMenu
            filters={table.filters}
            options={table.filterOptions}
            activeCount={table.activeFilterCount}
            onChange={table.setFilters}
            onClear={table.clearFilters}
          />
        </div>
      </header>
      {banner}
      <div
        ref={table.scrollRef}
        className="scrollbar-thin relative min-h-0 flex-1 overflow-x-hidden overflow-y-auto overscroll-contain [overflow-anchor:none]"
      >
        <div className="attempt-grid sticky top-0 z-10 border-y border-border/70 bg-background/92 px-3 py-1.5 text-[11px] font-medium text-subtle-foreground backdrop-blur-md">
          <span data-area="play" aria-hidden="true" />
          <SortHeader
            area="progress"
            sortKey="progress"
            current={table.sort}
            direction={table.direction}
            onSort={table.toggleSort}
            align="end"
          >
            {t("sort.progress")}
          </SortHeader>
          <SortHeader
            area="acc"
            sortKey="accuracy"
            current={table.sort}
            direction={table.direction}
            onSort={table.toggleSort}
            align="end"
          >
            {t("run.xAccuracyShort")}
          </SortHeader>
          <SortHeader
            area="pitch"
            sortKey="pitch"
            current={table.sort}
            direction={table.direction}
            onSort={table.toggleSort}
            align="end"
          >
            {t("sort.pitch")}
          </SortHeader>
          <span data-area="judg" aria-hidden="true" className="truncate">
            {t("attempts.judgments")}
          </span>
          <SortHeader
            area="time"
            sortKey="time"
            current={table.sort}
            direction={table.direction}
            onSort={table.toggleSort}
            align="end"
            title={offset}
          >
            {t("attempts.started")}
          </SortHeader>
          <span data-area="timeline" aria-hidden="true" className="truncate">
            {t("attempts.timeline")}
            {timelinesWaiting ? (
              <span className="text-subtle-foreground/80"> · {t("attempts.timelineWaiting")}</span>
            ) : null}
          </span>
          <span data-area="flags" aria-hidden="true" />
          <span data-area="lock" aria-hidden="true" />
          <span data-area="menu" aria-hidden="true" />
        </div>
        <ul
          ref={table.listRef}
          aria-label={t("attempts.listLabel")}
          className="relative"
          style={{ height: sortedRuns.length * rowStride }}
        >
          {sortedRuns.slice(renderedRange.start, renderedRange.end).map((run, offsetIndex) => {
            const index = renderedRange.start + offsetIndex;
            return (
              <AttemptRow
                key={run.id}
                run={run}
                offsetY={index * rowStride}
                rowRef={offsetIndex === 0 ? table.measureRow : undefined}
                selected={selectedRun?.id === run.id}
                focusable={focusableRunId === run.id}
                competitiveLayout={table.judgmentLayout.competitive}
                timeZone={timeZone}
                replayContext={replayContext}
                menuHandlers={menuHandlers}
                onSelect={toggleRun}
                onSetLocked={setLocked}
                onKeyDown={rowKeyDown}
              />
            );
          })}
        </ul>
        {sortedRuns.length === 0 ? (
          loading ? (
            <div className="grid gap-px px-3 pt-2" aria-hidden="true">
              {[0, 1, 2, 3].map((row) => (
                <Skeleton key={row} className="h-11 rounded-md opacity-60" />
              ))}
            </div>
          ) : (
            <div className="grid justify-items-center gap-3 px-4 py-10 text-center text-sm text-muted-foreground">
              <p>{table.activeFilterCount > 0 ? t("filter.noMatches") : t("attempts.empty")}</p>
              {table.activeFilterCount > 0 ? (
                <Button size="sm" variant="secondary" onClick={table.clearFilters}>
                  {t("filter.clear")}
                </Button>
              ) : null}
            </div>
          )
        ) : null}
      </div>
    </section>
  );
}

function SortHeader({
  area,
  sortKey,
  current,
  direction,
  onSort,
  align = "start",
  title,
  children,
}: {
  area: string;
  sortKey: AttemptSortKey;
  current: AttemptSortKey;
  direction: SortDirection;
  onSort: (key: AttemptSortKey) => void;
  align?: "start" | "end";
  title?: string;
  children: ReactNode;
}) {
  const { t } = useTranslation("activity");
  const active = current === sortKey;
  const label = `${t(`sort.${sortKey}`)}${
    active ? `, ${t(direction === "asc" ? "run.ascending" : "run.descending")}` : ""
  }`;
  // Right-aligned columns put the sort icon first so the label lines up with the numbers below.
  const icon = (
    <HugeiconsIcon
      aria-hidden="true"
      icon={active ? (direction === "asc" ? ArrowUp01Icon : ArrowDown01Icon) : UnfoldMoreIcon}
      size={12}
      strokeWidth={2.2}
      className={cn(
        "shrink-0 transition-opacity",
        active ? "opacity-100" : "opacity-50 group-hover/sort:opacity-90",
      )}
    />
  );
  return (
    <button
      type="button"
      data-area={area}
      aria-label={label}
      aria-pressed={active}
      title={title}
      onClick={() => onSort(sortKey)}
      className={cn(
        "group/sort -mx-1.5 inline-flex min-w-0 items-center gap-1 rounded-md px-1.5 py-1 transition-colors hover:bg-foreground/8 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
        align === "end" && "justify-self-end",
        active && "text-foreground",
      )}
    >
      {align === "end" ? icon : null}
      <span className="truncate">{children}</span>
      {align === "end" ? null : icon}
    </button>
  );
}
