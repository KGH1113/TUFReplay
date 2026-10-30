import {
  type KeyboardEvent,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useStableCallback } from "@/hooks/shared/use-stable-callback";
import type { ActivityRun } from "@/models/activity/activity-model";
import {
  type AttemptFilters,
  activeAttemptFilterCount,
  applyAttemptFilters,
  attemptFilterOptions,
  noAttemptFilters,
} from "@/models/activity/attempt-filters";
import { judgmentDisplayItems } from "@/models/activity/judgment-display";
import {
  anchoredRunScrollTop,
  calculateVirtualRunRange,
  type VirtualRunRange,
} from "@/models/activity/run-list-virtualization";
import { runEndPercent } from "@/models/activity/run-progress";

export type AttemptSortKey = "progress" | "time" | "pitch" | "accuracy";
export type SortDirection = "asc" | "desc";

type PendingSortLayout = { positions: Map<string, DOMRect>; scrollTop: number };
type ListLayout = {
  runs: ActivityRun[];
  filterFloor: number | null;
  filters: AttemptFilters;
  sort: AttemptSortKey;
  direction: SortDirection;
  stride: number;
  scrollTop: number;
};

const estimatedRowStride = 48;
const rowOverscan = 6;

export function useAttemptsTable({
  runs,
  scopeKey,
  filterFloor,
  selectedRunId,
  onSelectRun,
  onOpenRun,
}: {
  runs: ActivityRun[];
  /** Identifies the level being shown; attribute filters reset when it changes. */
  scopeKey: string;
  /** Start tile to show, or null for every attempt. */
  filterFloor: number | null;
  selectedRunId: string | null;
  onSelectRun: (run: ActivityRun | null) => void;
  onOpenRun: (run: ActivityRun) => void;
}) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const rowObserverRef = useRef<ResizeObserver | null>(null);
  const pendingSortLayoutRef = useRef<PendingSortLayout | null>(null);
  const listLayoutRef = useRef<ListLayout | null>(null);
  const pendingFocusRunIdRef = useRef<string | null>(null);
  const [sort, setSort] = useState<AttemptSortKey>("time");
  const [direction, setDirection] = useState<SortDirection>("desc");
  const [rowStride, setRowStride] = useState(estimatedRowStride);
  const [range, setRange] = useState<VirtualRunRange>({ start: 0, end: 0 });
  const [filters, setFilters] = useState<AttemptFilters>(noAttemptFilters);
  const [filterScope, setFilterScope] = useState(scopeKey);
  const selectRun = useStableCallback(onSelectRun);
  const openRun = useStableCallback(onOpenRun);

  // Pitches and difficulties differ per level, so a new level starts unfiltered.
  if (filterScope !== scopeKey) {
    setFilterScope(scopeKey);
    setFilters(noAttemptFilters);
  }
  const filterOptions = useMemo(() => attemptFilterOptions(runs), [runs]);
  const activeFilterCount = activeAttemptFilterCount(filters);
  const visibleRuns = useMemo(
    () =>
      applyAttemptFilters(
        filterFloor === null ? runs : runs.filter((run) => run.startTile === filterFloor),
        filters,
      ),
    [runs, filterFloor, filters],
  );
  const sortedRuns = useMemo(
    () => sortRuns(visibleRuns, sort, direction),
    [visibleRuns, sort, direction],
  );
  const renderedRange =
    range.end > range.start && range.start < sortedRuns.length
      ? { start: range.start, end: Math.min(range.end, sortedRuns.length) }
      : calculateVirtualRunRange(sortedRuns.length, 0, 0, rowStride, rowOverscan);

  const captureSortLayout = useCallback(() => {
    const list = listRef.current;
    const scroller = scrollRef.current;
    if (!list || !scroller || prefersReducedMotion()) return;
    const positions = new Map<string, DOMRect>();
    for (const element of list.querySelectorAll<HTMLElement>("[data-run-id]")) {
      const runId = element.dataset.runId;
      if (!runId) continue;
      for (const animation of element.getAnimations())
        if (animation.id === "attempt-sort") animation.cancel();
      positions.set(runId, element.getBoundingClientRect());
    }
    pendingSortLayoutRef.current = { positions, scrollTop: scroller.scrollTop };
  }, []);

  /** Clicking the active column flips direction; a new column starts from its most useful end. */
  const toggleSort = useCallback(
    (key: AttemptSortKey) => {
      captureSortLayout();
      if (key === sort) setDirection((current) => (current === "asc" ? "desc" : "asc"));
      else {
        setSort(key);
        setDirection("desc");
      }
    },
    [captureSortLayout, sort],
  );

  const measureRow = useCallback((element: HTMLLIElement | null) => {
    rowObserverRef.current?.disconnect();
    rowObserverRef.current = null;
    if (!element) return;
    const update = () => {
      const next = element.getBoundingClientRect().height;
      if (next > 0) setRowStride((current) => (Math.abs(current - next) < 0.5 ? current : next));
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(element);
    rowObserverRef.current = observer;
  }, []);
  useEffect(() => () => rowObserverRef.current?.disconnect(), []);

  // Keep the first visible row in place when runs arrive or leave above it.
  useLayoutEffect(() => {
    const scroller = scrollRef.current;
    const previous = listLayoutRef.current;
    if (
      scroller &&
      previous?.filterFloor === filterFloor &&
      previous.filters === filters &&
      previous.sort === sort &&
      previous.direction === direction
    ) {
      scroller.scrollTop = anchoredRunScrollTop(
        previous.runs,
        sortedRuns,
        previous.scrollTop,
        previous.stride,
        rowStride,
      );
    } else if (
      scroller &&
      previous &&
      (previous.filterFloor !== filterFloor || previous.filters !== filters)
    ) {
      scroller.scrollTop = 0;
    }
    listLayoutRef.current = {
      runs: sortedRuns,
      filterFloor,
      filters,
      sort,
      direction,
      stride: rowStride,
      scrollTop: scroller?.scrollTop ?? 0,
    };
  }, [direction, filterFloor, filters, rowStride, sort, sortedRuns]);

  useLayoutEffect(() => {
    const scroller = scrollRef.current;
    const list = listRef.current;
    if (!scroller || !list) return;
    let frame = 0;
    const update = () => {
      if (listLayoutRef.current) listLayoutRef.current.scrollTop = scroller.scrollTop;
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        const next = calculateVirtualRunRange(
          sortedRuns.length,
          scroller.scrollTop - list.offsetTop,
          scroller.clientHeight,
          rowStride,
          rowOverscan,
        );
        setRange((current) =>
          current.start === next.start && current.end === next.end ? current : next,
        );
      });
    };
    update();
    scroller.addEventListener("scroll", update, { passive: true });
    const observer = new ResizeObserver(update);
    observer.observe(scroller);
    return () => {
      cancelAnimationFrame(frame);
      scroller.removeEventListener("scroll", update);
      observer.disconnect();
    };
  }, [rowStride, sortedRuns.length]);

  // Slide rows from their old place to their new one after a sort change.
  useLayoutEffect(() => {
    void sort;
    void direction;
    const pending = pendingSortLayoutRef.current;
    pendingSortLayoutRef.current = null;
    const list = listRef.current;
    const scroller = scrollRef.current;
    if (!pending || !list || !scroller || prefersReducedMotion()) return;
    const scrollDelta = scroller.scrollTop - pending.scrollTop;
    for (const element of list.querySelectorAll<HTMLElement>("[data-run-id]")) {
      const previous = element.dataset.runId ? pending.positions.get(element.dataset.runId) : null;
      if (!previous) continue;
      const next = element.getBoundingClientRect();
      const y = previous.top - scrollDelta - next.top;
      if (Math.abs(y) < 0.5) continue;
      // `translate` composes with the row's virtual-list transform instead of replacing it.
      const animation = element.animate([{ translate: `0 ${y}px` }, { translate: "0 0" }], {
        duration: 320,
        easing: "cubic-bezier(0.22, 1, 0.36, 1)",
      });
      animation.id = "attempt-sort";
    }
  }, [direction, sort]);

  const scrollIndexIntoView = useCallback(
    (index: number) => {
      const scroller = scrollRef.current;
      const list = listRef.current;
      if (!scroller || !list) return;
      const header = list.offsetTop;
      const top = header + index * rowStride;
      const bottom = top + rowStride;
      if (top - header < scroller.scrollTop) scroller.scrollTop = top - header;
      else if (bottom > scroller.scrollTop + scroller.clientHeight)
        scroller.scrollTop = bottom - scroller.clientHeight;
    },
    [rowStride],
  );

  // Move keyboard focus to a row once it has been rendered by the virtual list.
  useLayoutEffect(() => {
    const runId = pendingFocusRunIdRef.current;
    if (!runId) return;
    const target = listRef.current?.querySelector<HTMLElement>(
      `[data-run-select="${CSS.escape(runId)}"]`,
    );
    if (!target) return;
    pendingFocusRunIdRef.current = null;
    target.focus({ preventScroll: true });
  });

  const selectIndex = useCallback(
    (index: number) => {
      const run = sortedRuns[index];
      if (!run) return;
      pendingFocusRunIdRef.current = run.id;
      scrollIndexIntoView(index);
      selectRun(run);
    },
    [scrollIndexIntoView, selectRun, sortedRuns],
  );

  const onKeyDown = useCallback(
    (event: KeyboardEvent<HTMLElement>) => {
      if (event.altKey || event.metaKey || event.ctrlKey) return;
      const target = event.target as HTMLElement;
      // Menus and buttons inside a row handle their own keys.
      if (target.closest("[role='menu'],[data-attempt-control]")) return;
      const current = sortedRuns.findIndex((run) => run.id === selectedRunId);
      if (event.key === "ArrowDown" || event.key === "ArrowUp") {
        event.preventDefault();
        const step = event.key === "ArrowDown" ? 1 : -1;
        const next =
          current < 0
            ? step > 0
              ? 0
              : sortedRuns.length - 1
            : Math.min(sortedRuns.length - 1, Math.max(0, current + step));
        selectIndex(next);
      } else if (event.key === "Home") {
        event.preventDefault();
        selectIndex(0);
      } else if (event.key === "End") {
        event.preventDefault();
        selectIndex(sortedRuns.length - 1);
      } else if (event.key === "Enter" && current >= 0) {
        event.preventDefault();
        openRun(sortedRuns[current]);
      } else if (event.key === "Escape" && current >= 0) {
        event.preventDefault();
        selectRun(null);
      }
    },
    [openRun, selectIndex, selectRun, selectedRunId, sortedRuns],
  );

  const judgmentLayout = useMemo(() => judgmentColumnLayout(sortedRuns), [sortedRuns]);

  return {
    scrollRef,
    listRef,
    measureRow,
    sort,
    direction,
    toggleSort,
    filters,
    setFilters,
    filterOptions,
    activeFilterCount,
    clearFilters: () => setFilters(noAttemptFilters),
    sortedRuns,
    renderedRange,
    rowStride,
    judgmentLayout,
    onKeyDown,
  };
}

/**
 * One column per judgment in the widest system present, each sized to its own largest count,
 * so a 4-digit Perfect column doesn't widen the rarely-used Miss or Too Early columns.
 */
function judgmentColumnLayout(runs: ActivityRun[]) {
  const competitive = runs.some((run) => run.judgmentSystem === "ModernCompetitive");
  const keys = judgmentDisplayItems(null, competitive ? "ModernCompetitive" : "Legacy").map(
    (item) => item.key,
  );
  const digits = new Map<string, number>(keys.map((key) => [key, 1]));
  for (const run of runs)
    for (const item of judgmentDisplayItems(run.judgmentCounts, run.judgmentSystem))
      if (digits.has(item.key))
        digits.set(item.key, Math.max(digits.get(item.key) ?? 1, String(item.value).length));
  // 12px tabular digits are about 0.45rem wide; keep at least a comfortable 1.5rem target.
  const widths = keys.map((key) => Math.max(1.5, 0.6 + (digits.get(key) ?? 1) * 0.45));
  return {
    competitive,
    count: keys.length,
    columns: widths.map((width) => `${width}rem`).join(" "),
    width: `${widths.reduce((sum, width) => sum + width, 0)}rem`,
  };
}

function prefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function sortRuns(runs: ActivityRun[], sort: AttemptSortKey, direction: SortDirection) {
  return [...runs].sort((left, right) => {
    const leftValue = sortValue(left, sort);
    const rightValue = sortValue(right, sort);
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

function sortValue(run: ActivityRun, sort: AttemptSortKey) {
  if (sort === "progress") return runEndPercent(run);
  if (sort === "pitch") return run.levelPitchPercent;
  if (sort === "accuracy") return Number.isFinite(run.xAccuracy) ? run.xAccuracy : null;
  const timestamp = Date.parse(run.startedAtUtc);
  return Number.isNaN(timestamp) ? null : timestamp;
}
