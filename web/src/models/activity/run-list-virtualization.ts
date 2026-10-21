export type VirtualRunRange = {
  start: number;
  end: number;
};

export function anchoredRunScrollTop(
  previousRuns: readonly { id: string }[],
  nextRuns: readonly { id: string }[],
  scrollTop: number,
  previousStride: number,
  nextStride: number,
) {
  if (scrollTop <= 0 || !previousRuns.length || !nextRuns.length) return 0;
  const safePreviousStride = Math.max(1, previousStride);
  const safeNextStride = Math.max(1, nextStride);
  const firstVisible = Math.min(
    previousRuns.length - 1,
    Math.floor(scrollTop / safePreviousStride),
  );
  const nextIndices = new Map(nextRuns.map((run, index) => [run.id, index]));
  // If the visible card was deleted, keep the nearest surviving card in place.
  for (let distance = 0; distance < previousRuns.length; distance++) {
    for (const index of distance === 0
      ? [firstVisible]
      : [firstVisible + distance, firstVisible - distance]) {
      const previousRun = previousRuns[index];
      if (!previousRun) continue;
      const nextIndex = nextIndices.get(previousRun.id);
      if (nextIndex === undefined) continue;
      return Math.max(0, nextIndex * safeNextStride + scrollTop - index * safePreviousStride);
    }
  }
  return 0;
}

export function calculateVirtualRunRange(
  itemCount: number,
  scrollTop: number,
  viewportHeight: number,
  itemStride: number,
  overscan: number,
): VirtualRunRange {
  if (itemCount <= 0) return { start: 0, end: 0 };

  const safeStride = Math.max(1, itemStride);
  const safeScrollTop = Math.max(0, scrollTop);
  const safeViewportHeight = Math.max(0, viewportHeight);
  const safeOverscan = Math.max(0, Math.floor(overscan));
  const firstVisible = Math.min(itemCount - 1, Math.floor(safeScrollTop / safeStride));
  const visibleCount = Math.max(1, Math.ceil(safeViewportHeight / safeStride) + 1);
  const start = Math.max(0, firstVisible - safeOverscan);
  const end = Math.min(itemCount, firstVisible + visibleCount + safeOverscan);

  return { start, end };
}
