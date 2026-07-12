import { useEffect, useRef } from "react";

import type { ActivityDay } from "../activity.model";
import { formatDayLabel } from "../lib/activity-date.utils";
import { cn } from "@/ui/ui-class.utils";

export function DayRail({ days, selectedDate, onSelectDate }: { days: ActivityDay[]; selectedDate: string | null; onSelectDate: (date: string) => void }) {
  const refs = useRef(new Map<string, HTMLButtonElement>());
  useEffect(() => { if (selectedDate) refs.current.get(selectedDate)?.scrollIntoView({ block: "nearest" }); }, [selectedDate]);
  return (
    <aside className="flex min-h-screen flex-col bg-muted/20">
      <div className="border-b border-border px-3 py-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Days</div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {days.map((day) => (
          <button key={day.date} ref={(node) => { if (node) refs.current.set(day.date, node); else refs.current.delete(day.date); }} type="button"
            className={cn("block w-full border-b border-border px-3 py-3 text-left transition hover:bg-muted/50", selectedDate === day.date && "bg-background shadow-[inset_3px_0_0_var(--primary)]")}
            onClick={() => onSelectDate(day.date)}>
            <div className="font-heading text-lg font-semibold">{formatDayLabel(day.date)}</div>
            <div className="mt-2 grid grid-cols-2 gap-x-2 gap-y-1 text-xs">
              <span className="text-muted-foreground">runs</span><span className="text-right font-medium">{day.runCount}</span>
              <span className="text-muted-foreground">levels</span><span className="text-right font-medium">{day.levelSessions.length}</span>
              <span className="text-muted-foreground">clears</span><span className="text-right font-medium">{day.clearRunCount}</span>
            </div>
          </button>
        ))}
      </div>
    </aside>
  );
}
