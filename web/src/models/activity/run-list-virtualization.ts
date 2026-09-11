export type VirtualRunRange = {
  start: number;
  end: number;
};

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
