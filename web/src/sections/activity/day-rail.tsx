import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { formatDayLabel } from "@/models/activity/activity-date";
import type { ActivityDay } from "@/models/activity/activity-model";
import { cn } from "@/shared/lib/cn";

export function DayRail({
  days,
  selectedDate,
  onSelectDate,
}: {
  days: ActivityDay[];
  selectedDate: string | null;
  onSelectDate: (date: string) => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const refs = useRef(new Map<string, HTMLButtonElement>());
  useEffect(() => {
    if (selectedDate) refs.current.get(selectedDate)?.scrollIntoView({ block: "nearest" });
  }, [selectedDate]);
  return (
    <aside className="flex min-h-0 flex-col bg-muted/20">
      <div className="border-b border-border px-3 py-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {t("days")}
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {days.map((day) => (
          <button
            key={day.date}
            ref={(node) => {
              if (node) refs.current.set(day.date, node);
              else refs.current.delete(day.date);
            }}
            type="button"
            className={cn(
              "block w-full border-b border-border px-3 py-3 text-left transition hover:bg-muted/50",
              selectedDate === day.date && "bg-background shadow-[inset_3px_0_0_var(--primary)]",
            )}
            onClick={() => onSelectDate(day.date)}
          >
            <div className="font-heading text-lg font-semibold">
              {formatDayLabel(day.date, locale)}
            </div>
            <div className="mt-2 grid grid-cols-2 gap-x-2 gap-y-1 text-xs">
              <span className="text-muted-foreground">
                {t("counts.runs", { count: day.runCount })}
              </span>
              <span className="text-right font-medium">{day.runCount}</span>
              <span className="text-muted-foreground">
                {t("counts.levels", { count: day.levelSessions.length })}
              </span>
              <span className="text-right font-medium">{day.levelSessions.length}</span>
              <span className="text-muted-foreground">
                {t("counts.clears", { count: day.clearRunCount })}
              </span>
              <span className="text-right font-medium">{day.clearRunCount}</span>
            </div>
          </button>
        ))}
      </div>
    </aside>
  );
}
