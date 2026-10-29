import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { DayDeleteDialog, type DayDeleteOutcome } from "@/components/activity/day-delete-dialog";
import { DayRailRow } from "@/components/activity/day-rail-row";
import { dayStampParts } from "@/models/activity/activity-date";
import type { ActivityDay } from "@/models/activity/activity-model";

export function DayRail({
  days,
  selectedDate,
  todayDate,
  readOnly,
  onSelectDate,
  onDeleteDay,
}: {
  days: ActivityDay[];
  selectedDate: string | null;
  todayDate: string;
  readOnly: boolean;
  onSelectDate: (date: string) => void;
  onDeleteDay: (day: ActivityDay, keepClears: boolean) => Promise<DayDeleteOutcome>;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const refs = useRef(new Map<string, HTMLButtonElement>());
  const returnFocus = useRef<HTMLElement | null>(null);
  // Kept after closing so the dialog can animate out even when its day was just deleted.
  const [pending, setPending] = useState<ActivityDay | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const pendingDay = days.find((day) => day.date === pending?.date) ?? pending;
  const months = useMemo(() => groupByMonth(days, locale), [days, locale]);

  useEffect(() => {
    if (selectedDate) refs.current.get(selectedDate)?.scrollIntoView({ block: "nearest" });
  }, [selectedDate]);

  const registerButton = useCallback((date: string, node: HTMLButtonElement | null) => {
    if (node) refs.current.set(date, node);
    else refs.current.delete(date);
  }, []);
  const requestDelete = useCallback((day: ActivityDay, focusTarget: HTMLElement | null) => {
    returnFocus.current = focusTarget;
    setPending(day);
    setDialogOpen(true);
  }, []);
  const announceDeleted = (day: ActivityDay, outcome: DayDeleteOutcome) => {
    const date = dayStampParts(day.date, locale).shortLabel;
    const kept = outcome.keptLockedRunCount;
    const description = [
      kept > 0
        ? t("dayDelete.doneKept", { count: kept, date })
        : t("dayDelete.doneEmpty", { date }),
      outcome.keptInUseRunCount > 0 ? t("dayDelete.doneInUse") : "",
    ]
      .filter(Boolean)
      .join(" ");
    toast.success(t("dayDelete.done", { count: outcome.deletedRunCount }), { description });
  };

  return (
    <aside className="glass-structural flex min-h-0 flex-col overflow-hidden rounded-2xl">
      <div className="px-3 pb-1 pt-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {t("days")}
      </div>
      <div className="relative min-h-0 flex-1 overflow-y-auto px-1.5 pb-1.5 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {months.map((month) => (
          <section key={month.key} aria-label={month.label}>
            <h3 className="sticky top-0 z-10 -mx-1.5 bg-linear-to-b from-[color-mix(in_oklab,var(--card)_92%,var(--background))] from-70% to-transparent px-3.5 pb-2 pt-2 text-[11px] font-bold uppercase tracking-[0.08em] text-muted-foreground">
              {month.label}
            </h3>
            <ul className="grid gap-0.5">
              {month.days.map((day) => (
                <DayRailRow
                  key={day.date}
                  day={day}
                  selected={selectedDate === day.date}
                  today={todayDate === day.date}
                  readOnly={readOnly}
                  registerButton={registerButton}
                  onSelect={onSelectDate}
                  onRequestDelete={requestDelete}
                />
              ))}
            </ul>
          </section>
        ))}
      </div>
      <DayDeleteDialog
        day={pendingDay}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onDelete={onDeleteDay}
        onDeleted={announceDeleted}
        restoreFocus={() => {
          const target = returnFocus.current;
          returnFocus.current = null;
          // The deleted day may be gone; fall back to the newly selected day.
          if (target?.isConnected) target.focus();
          else if (selectedDate) refs.current.get(selectedDate)?.focus();
        }}
      />
    </aside>
  );
}

function groupByMonth(days: ActivityDay[], locale: string) {
  const months: { key: string; label: string; days: ActivityDay[] }[] = [];
  for (const day of days) {
    const stamp = dayStampParts(day.date, locale);
    const last = months[months.length - 1];
    if (last?.key === stamp.monthKey) last.days.push(day);
    else months.push({ key: stamp.monthKey, label: stamp.monthLabel, days: [day] });
  }
  return months;
}
