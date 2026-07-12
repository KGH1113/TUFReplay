import { Calendar03Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useMemo, useRef, useState } from "react";

import { mockActivityDays } from "../activity.mock";
import type { ActivityDaySummary } from "../activity.model";
import {
  addMonths,
  formatDateKey,
  formatDayLabel,
  formatMonthLabel,
  getCalendarWeeks,
  getMonthStart,
  parseDate,
} from "../lib/activity-date.utils";
import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle, DialogTrigger } from "@/ui/dialog.component";
import { cn } from "@/ui/ui-class.utils";

export function DayRail({
  selectedDate,
  selectedDay,
  onSelectDate,
}: {
  selectedDate: string;
  selectedDay: ActivityDaySummary;
  onSelectDate: (date: string) => void;
}) {
  const dayButtonRefs = useRef(new Map<string, HTMLButtonElement>());

  useEffect(() => {
    dayButtonRefs.current.get(selectedDate)?.scrollIntoView({ block: "start" });
  }, [selectedDate]);

  return (
    <aside className="flex min-h-screen flex-col bg-muted/20">
      <div className="border-b border-border px-3 py-3">
        <div className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Days</div>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {mockActivityDays.map((day) => (
          <button
            key={day.Date}
            ref={(element) => {
              if (element) dayButtonRefs.current.set(day.Date, element);
              else dayButtonRefs.current.delete(day.Date);
            }}
            type="button"
            className={cn(
              "block w-full border-b border-border px-3 py-3 text-left transition hover:bg-muted/50",
              selectedDate === day.Date && "bg-background shadow-[inset_3px_0_0_var(--primary)]",
            )}
            onClick={() => onSelectDate(day.Date)}
          >
            <div className="font-heading text-lg font-semibold">{formatDayLabel(day.Date)}</div>
            <div className="mt-2 grid grid-cols-2 gap-x-2 gap-y-1 text-xs">
              <span className="text-muted-foreground">runs</span>
              <span className="text-right font-medium">{day.RunCount}</span>
              <span className="text-muted-foreground">levels</span>
              <span className="text-right font-medium">{day.LevelSessionCount}</span>
              <span className="text-muted-foreground">clears</span>
              <span className="text-right font-medium">{day.ClearRunCount}</span>
            </div>
          </button>
        ))}
      </div>
      <div className="mt-auto border-t border-border p-2">
        <ActivityDatePicker selectedDate={selectedDate} selectedDay={selectedDay} onSelectDate={onSelectDate} />
      </div>
    </aside>
  );
}

function ActivityDatePicker({
  selectedDate,
  selectedDay,
  onSelectDate,
}: {
  selectedDate: string;
  selectedDay: ActivityDaySummary;
  onSelectDate: (date: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [visibleMonth, setVisibleMonth] = useState(() => getMonthStart(parseDate(selectedDate) ?? new Date()));
  const recordedDates = useMemo(() => new Set(mockActivityDays.map((day) => day.Date)), []);
  const todayDate = formatDateKey(new Date());
  const weeks = getCalendarWeeks(visibleMonth);

  useEffect(() => {
    if (open) setVisibleMonth(getMonthStart(parseDate(selectedDate) ?? new Date()));
  }, [open, selectedDate]);

  const selectDate = (date: string) => {
    if (!recordedDates.has(date)) return;

    onSelectDate(date);
    setOpen(false);
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" className="h-auto w-full justify-start rounded-md px-2 py-2 text-left">
          <HugeiconsIcon icon={Calendar03Icon} data-icon="inline-start" />
          <span className="min-w-0">
            <span className="block truncate text-xs font-medium">{selectedDay.Date}</span>
          </span>
        </Button>
      </DialogTrigger>
      <DialogContent className="w-[min(22rem,calc(100vw-2rem))]">
        <div className="flex items-center justify-between gap-2">
          <Button variant="outline" size="icon-xs" onClick={() => setVisibleMonth(addMonths(visibleMonth, -1))}>
            &lt;
          </Button>
          <DialogTitle className="flex-1 text-center">{formatMonthLabel(visibleMonth)}</DialogTitle>
          <Button variant="outline" size="icon-xs" onClick={() => setVisibleMonth(addMonths(visibleMonth, 1))}>
            &gt;
          </Button>
        </div>
        <div className="mt-4 grid grid-cols-7 gap-1 text-center text-xs text-muted-foreground">
          {["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].map((dayName) => (
            <div key={dayName} className="py-1">
              {dayName}
            </div>
          ))}
        </div>
        <div className="mt-1 grid grid-cols-7 gap-1">
          {weeks.flat().map((day) => {
            const date = formatDateKey(day);
            const isCurrentMonth = day.getMonth() === visibleMonth.getMonth();
            const isRecorded = recordedDates.has(date);
            const isSelected = date === selectedDate;
            const isToday = date === todayDate;

            return (
              <button
                key={date}
                type="button"
                disabled={!isRecorded}
                className={cn(
                  "grid h-8 place-items-center rounded-md border border-transparent text-sm transition",
                  isCurrentMonth ? "text-foreground" : "text-muted-foreground/40",
                  isRecorded && "hover:border-primary/50 hover:bg-primary/10",
                  isToday && !isSelected && "border-primary/70",
                  isSelected && "border-primary bg-primary text-primary-foreground hover:bg-primary",
                  !isRecorded && !isToday && "cursor-not-allowed opacity-30",
                  !isRecorded && isToday && "cursor-not-allowed opacity-70",
                )}
                onClick={() => selectDate(date)}
              >
                {day.getDate()}
              </button>
            );
          })}
        </div>
      </DialogContent>
    </Dialog>
  );
}
