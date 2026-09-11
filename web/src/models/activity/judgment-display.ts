import type { JudgmentCounts } from "@/models/activity/activity-model";

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
] as const satisfies readonly { key: keyof JudgmentCounts; color: string }[];

const competitiveJudgmentDisplayOrder = [
  { key: "Overload", color: "#D958FF" },
  { key: "TooEarly", color: "#FF0000" },
  { key: "Early", color: "#FF6F4E" },
  { key: "EarlyPerfect", color: "#A0FF4E" },
  { key: "PerfectMinus", color: "#60FF4E" },
  { key: "XPerfect", color: "#60FF4E" },
  { key: "PerfectPlus", color: "#60FF4E" },
  { key: "LatePerfect", color: "#A0FF4E" },
  { key: "Late", color: "#FF6F4E" },
  { key: "TooLate", color: "#FF0000" },
  { key: "Miss", color: "#D958FF" },
] as const satisfies readonly { key: keyof JudgmentCounts; color: string }[];

export function judgmentDisplayItems(
  counts?: Partial<JudgmentCounts> | null,
  judgmentSystem: "Legacy" | "ModernClassic" | "ModernCompetitive" = "Legacy",
) {
  const order =
    judgmentSystem === "ModernCompetitive" ? competitiveJudgmentDisplayOrder : judgmentDisplayOrder;
  return order.map((item) => ({
    ...item,
    value: normalizeCount(counts?.[item.key]),
  }));
}

function normalizeCount(value: number | undefined) {
  return Number.isFinite(value) ? Math.max(0, Math.trunc(value ?? 0)) : 0;
}
