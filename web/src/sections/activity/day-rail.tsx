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
    <aside className="glass-structural flex min-h-0 flex-col overflow-hidden rounded-2xl">
      <div className="px-3 pb-2 pt-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {t("days")}
      </div>
      <div className="min-h-0 flex-1 space-y-1 overflow-y-auto p-1.5 pt-0 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {days.map((day) => (
          <button
            key={day.date}
            ref={(node) => {
              if (node) refs.current.set(day.date, node);
              else refs.current.delete(day.date);
            }}
            type="button"
            className={cn(
              "relative block w-full overflow-hidden rounded-xl px-2.5 py-3 text-left transition-[background-color,box-shadow] hover:bg-background/35",
              selectedDate === day.date &&
                "bg-background/70 shadow-[0_8px_20px_rgb(0_0_0/0.16)] before:pointer-events-none before:absolute before:inset-y-3 before:left-0 before:w-1 before:rounded-r-full before:bg-primary before:content-['']",
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
