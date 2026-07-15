import type { ActivityJudgmentCounts } from "../activity.model";

export const judgmentDisplayOrder = [
  { key: "Overload", label: "Overload", color: "#D958FF" },
  { key: "TooEarly", label: "Too Early", color: "#FF0000" },
  { key: "Early", label: "Early", color: "#FF6F4E" },
  { key: "EarlyPerfect", label: "Early Perfect", color: "#A0FF4E" },
  { key: "Perfect", label: "Perfect", color: "#60FF4E" },
  { key: "LatePerfect", label: "Late Perfect", color: "#A0FF4E" },
  { key: "Late", label: "Late", color: "#FF6F4E" },
  { key: "TooLate", label: "Too Late", color: "#FF0000" },
  { key: "Miss", label: "Miss", color: "#D958FF" },
] as const satisfies readonly { key: keyof ActivityJudgmentCounts; label: string; color: string }[];

export function judgmentDisplayItems(counts?: Partial<ActivityJudgmentCounts> | null) {
  return judgmentDisplayOrder.map((item) => ({
    ...item,
    value: normalizeCount(counts?.[item.key]),
  }));
}

function normalizeCount(value: number | undefined) {
  return Number.isFinite(value) ? Math.max(0, Math.trunc(value ?? 0)) : 0;
}
