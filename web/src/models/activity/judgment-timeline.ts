import { judgmentDisplayOrder } from "@/models/activity/judgment-display";

/** Judgments the timeline marks; Perfect and its competitive variants are left out on purpose. */
export type TimelineJudgmentKind =
  | "Overload"
  | "TooEarly"
  | "Early"
  | "EarlyPerfect"
  | "LatePerfect"
  | "Late"
  | "TooLate"
  | "Miss";

export interface RunJudgmentTimeline {
  runId: string;
  durationMs: number;
  /** Milliseconds from the start of the run, one per judgment. */
  timesMs: number[];
  kinds: TimelineJudgmentKind[];
}

const severity: Record<TimelineJudgmentKind, number> = {
  EarlyPerfect: 1,
  LatePerfect: 1,
  Early: 2,
  Late: 2,
  TooEarly: 3,
  TooLate: 3,
  Overload: 4,
  Miss: 5,
};

const colors = new Map<string, string>(
  judgmentDisplayOrder.map((item) => [item.key, item.color] as const),
);

export function timelineJudgmentColor(kind: TimelineJudgmentKind) {
  return colors.get(kind) ?? "currentColor";
}

/**
 * Splits the run into equal columns and keeps the most severe judgment in each, as the in-game
 * timeline does, so a miss is never hidden behind a nearby early or late.
 */
export function judgmentTimelineColumns(
  timeline: RunJudgmentTimeline,
  columnCount: number,
): (TimelineJudgmentKind | null)[] {
  const columns: (TimelineJudgmentKind | null)[] = Array.from({ length: columnCount }, () => null);
  if (columnCount <= 0 || timeline.durationMs <= 0) return columns;
  const count = Math.min(timeline.timesMs.length, timeline.kinds.length);
  for (let index = 0; index < count; index++) {
    const kind = timeline.kinds[index];
    const fraction = Math.min(1, Math.max(0, timeline.timesMs[index] / timeline.durationMs));
    const column = Math.min(columnCount - 1, Math.floor(fraction * columnCount));
    const current = columns[column];
    if (current === null || severity[kind] > severity[current]) columns[column] = kind;
  }
  return columns;
}
