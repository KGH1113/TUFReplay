import { ArrowDown01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { DayDeleteDialog, type DayDeleteOutcome } from "@/components/activity/day-delete-dialog";
import { DayListRow } from "@/components/activity/day-list-row";
import { useScrollEdges } from "@/hooks/shared/use-scroll-edges";
import { dayStampParts } from "@/models/activity/activity-date";
import type { ActivityDay, LevelCard, LevelMetadata } from "@/models/activity/activity-model";
import { cn } from "@/shared/lib/cn";

/** Days grouped by month; the selected day lists its levels underneath. */
export function DayList({
  days,
  selectedDate,
  selectedLevelGroupId,
  todayDate,
  timeZone,
  readOnly,
  metadataFor,
  onSelectLevel,
  onDeleteDay,
}: {
  days: ActivityDay[];
  selectedDate: string | null;
  selectedLevelGroupId: string | null;
  todayDate: string;
  timeZone: string;
  readOnly: boolean;
  metadataFor: (level: LevelCard) => LevelMetadata;
  onSelectLevel: (date: string, levelGroupId: string) => void;
  onDeleteDay: (day: ActivityDay, keepClears: boolean) => Promise<DayDeleteOutcome>;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const refs = useRef(new Map<string, HTMLButtonElement>());
  const scrollRef = useRef<HTMLDivElement>(null);
  // Days open and close independently; the day holding the selected level always starts open.
  const [expandedDates, setExpandedDates] = useState<ReadonlySet<string>>(
    () => new Set(selectedDate ? [selectedDate] : []),
  );
  const [openedForDate, setOpenedForDate] = useState(selectedDate);
  if (openedForDate !== selectedDate) {
    setOpenedForDate(selectedDate);
    if (selectedDate && !expandedDates.has(selectedDate))
      setExpandedDates(new Set([...expandedDates, selectedDate]));
  }
  const toggleExpanded = useCallback((date: string) => {
    setExpandedDates((current) => {
      const next = new Set(current);
      if (next.has(date)) next.delete(date);
      else next.add(date);
      return next;
    });
  }, []);
  const edges = useScrollEdges(scrollRef);
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
    <nav aria-label={t("days")} className="relative min-h-0 flex-1">
      <div
        ref={scrollRef}
        className={cn(
          "scrollbar-thin h-full overflow-x-hidden overflow-y-auto overscroll-contain px-2 pb-3",
          edges.below &&
            "[mask-image:linear-gradient(to_bottom,black_calc(100%-3.5rem),transparent)]",
        )}
      >
        {months.map((month) => (
          <section key={month.key} aria-label={month.label}>
            <h3 className="sticky top-0 z-10 bg-linear-to-b from-sidebar from-75% to-transparent px-2 pt-3 pb-2 text-[11px] font-semibold tracking-[0.06em] text-subtle-foreground uppercase">
              {month.label}
            </h3>
            <ul className="grid min-w-0 grid-cols-1 gap-0.5">
              {month.days.map((day) => (
                <DayListRow
                  key={day.date}
                  day={day}
                  selected={selectedDate === day.date}
                  expanded={expandedDates.has(day.date)}
                  today={todayDate === day.date}
                  readOnly={readOnly}
                  selectedLevelGroupId={selectedLevelGroupId}
                  timeZone={timeZone}
                  metadataFor={metadataFor}
                  registerButton={registerButton}
                  onToggle={toggleExpanded}
                  onSelectLevel={onSelectLevel}
                  onRequestDelete={requestDelete}
                />
              ))}
            </ul>
          </section>
        ))}
        {days.length === 0 ? (
          <p className="px-3 py-6 text-xs leading-relaxed text-muted-foreground">
            {t("sidebar.noDays")}
          </p>
        ) : null}
      </div>
      {edges.below ? (
        <button
          type="button"
          onClick={() =>
            scrollRef.current?.scrollBy({
              top: scrollRef.current.clientHeight * 0.8,
              behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches
                ? "auto"
                : "smooth",
            })
          }
          className="material-chrome absolute bottom-2 left-1/2 inline-flex -translate-x-1/2 items-center gap-1 rounded-full px-3 py-1 text-[11px] font-medium text-foreground/85 transition-[transform,background-color] hover:text-foreground active:scale-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring motion-reduce:active:scale-100"
        >
          {t("sidebar.moreDays")}
          <HugeiconsIcon aria-hidden="true" icon={ArrowDown01Icon} size={12} strokeWidth={2.2} />
        </button>
      ) : null}
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
    </nav>
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
