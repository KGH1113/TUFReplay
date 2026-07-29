import type { ActivityJudgmentCounts } from "../activity.model";

export const judgmentDisplayOrder = [
  { key: "Overload", color: "#D958FF" },
  { key: "TooEarly", color: "#FF0000" },
  { key: "Early", color: "#FF6F4E" },
  { key: "EarlyPerfect", color: "#A0FF4E" },
  { key: "Perfect", color: "#60FF4E" },
  { key: "LatePerfect", color: "#A0FF4E" },
  { key: "Late", color: "#FF6F4E" },
  { key: "TooLate", color: "#FF0000" },
  { key: "Miss", color: "#D958FF" },
] as const satisfies readonly { key: keyof ActivityJudgmentCounts; color: string }[];

export function judgmentDisplayItems(counts?: Partial<ActivityJudgmentCounts> | null) {
  return judgmentDisplayOrder.map((item) => ({
    ...item,
    value: normalizeCount(counts?.[item.key]),
  }));
}

function normalizeCount(value: number | undefined) {
  return Number.isFinite(value) ? Math.max(0, Math.trunc(value ?? 0)) : 0;
}
