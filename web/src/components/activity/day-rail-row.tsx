import {
  Delete02Icon,
  MoreVerticalIcon,
  SquareLock02Icon,
  Tick02Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { memo, type ReactNode, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { dayStampParts } from "@/models/activity/activity-date";
import type { ActivityDay } from "@/models/activity/activity-model";
import { dayDeleteBlocker } from "@/models/activity/day-deletion";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuTrigger,
} from "@/shared/ui/context-menu";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/shared/ui/dropdown-menu";

export const DayRailRow = memo(function DayRailRow({
  day,
  selected,
  today,
  readOnly,
  registerButton,
  onSelect,
  onRequestDelete,
}: {
  day: ActivityDay;
  selected: boolean;
  today: boolean;
  readOnly: boolean;
  registerButton: (date: string, node: HTMLButtonElement | null) => void;
  onSelect: (date: string) => void;
  onRequestDelete: (day: ActivityDay, returnFocus: HTMLElement | null) => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const stamp = dayStampParts(day.date, locale);
  const [menuOpen, setMenuOpen] = useState(false);
  const blocker = dayDeleteBlocker(day);
  const deleteDisabled = readOnly || blocker !== null;
  const levelCount = day.levelSessions.length;
  const label = [
    `${stamp.fullLabel}${today ? `, ${t("dayRail.today")}` : ""}`,
    `${day.runCount} ${t("counts.runs", { count: day.runCount })}`,
    `${levelCount} ${t("counts.levels", { count: levelCount })}`,
    `${day.clearRunCount} ${t("counts.clears", { count: day.clearRunCount })}`,
    day.lockedRunCount ? t("dayRail.locked", { count: day.lockedRunCount }) : "",
  ]
    .filter(Boolean)
    .join(", ");
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  const requestDelete = (returnFocus: HTMLElement | null) => {
    if (!deleteDisabled) onRequestDelete(day, returnFocus);
  };
  const deleteLabel = (
    <DeleteLabel hint={blocker ? t(`dayDelete.blocked.${blocker}`) : null}>
      {t("dayDelete.action")}
    </DeleteLabel>
  );

  return (
    <ContextMenu>
      <ContextMenuTrigger asChild disabled={readOnly}>
        <li className="group/day relative">
          <button
            ref={(node) => {
              buttonRef.current = node;
              registerButton(day.date, node);
            }}
            type="button"
            aria-current={selected ? "date" : undefined}
            aria-label={label}
            onClick={() => onSelect(day.date)}
            onKeyDown={(event) => {
              if (event.key !== "Delete" && event.key !== "Backspace") return;
              event.preventDefault();
              requestDelete(event.currentTarget);
            }}
            className={cn(
              "relative grid w-full grid-cols-[2.25rem_minmax(0,1fr)] items-center gap-2.5 rounded-xl py-2.5 pl-2.5 pr-2 text-left transition-[background-color,box-shadow,transform] duration-150 hover:bg-background/35 active:scale-[0.985] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:active:scale-100",
              selected &&
                "bg-background/70 shadow-[0_8px_20px_rgb(0_0_0/0.16)] before:pointer-events-none before:absolute before:inset-y-3 before:left-0 before:w-1 before:rounded-r-full before:bg-primary before:content-['']",
            )}
          >
            <span aria-hidden="true" className="grid justify-items-center leading-none">
              <span
                className={cn(
                  "text-[9.5px] font-bold uppercase tracking-[0.08em] text-muted-foreground",
                  today && "text-primary",
                )}
              >
                {stamp.weekday}
              </span>
              <span
                className={cn(
                  "mt-1 font-heading text-2xl font-bold tabular-nums tracking-[-0.03em]",
                  today && "text-primary",
                )}
              >
                {stamp.day}
              </span>
            </span>
            <span aria-hidden="true" className="grid min-w-0 gap-px text-xs text-muted-foreground">
              <DayStat value={day.runCount} unit={t("counts.runs", { count: day.runCount })} />
              <DayStat value={levelCount} unit={t("counts.levels", { count: levelCount })} />
              <span className="flex min-w-0 items-center justify-between gap-1">
                <span
                  className={cn(
                    "inline-flex min-w-0 items-center gap-0.5 truncate",
                    day.clearRunCount > 0
                      ? "font-semibold text-primary"
                      : "text-muted-foreground/60",
                  )}
                >
                  {day.clearRunCount > 0 ? (
                    <HugeiconsIcon icon={Tick02Icon} size={11} strokeWidth={2.6} />
                  ) : null}
                  <span className="tabular-nums">{day.clearRunCount}</span>{" "}
                  {t("counts.clears", { count: day.clearRunCount })}
                </span>
                {day.lockedRunCount > 0 ? (
                  <span className="inline-flex shrink-0 items-center gap-0.5 text-[11px] tabular-nums">
                    <HugeiconsIcon icon={SquareLock02Icon} size={11} strokeWidth={2} />
                    {day.lockedRunCount}
                  </span>
                ) : null}
              </span>
            </span>
          </button>
          <DropdownMenu open={menuOpen} onOpenChange={setMenuOpen}>
            <DropdownMenuTrigger asChild>
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                aria-label={t("dayRail.openActions", { date: stamp.fullLabel })}
                className={cn(
                  "absolute right-1.5 top-1.5 text-muted-foreground opacity-0 transition-opacity hover:text-foreground focus-visible:opacity-100 group-hover/day:opacity-100 data-[state=open]:opacity-100 [@media(hover:none)]:opacity-100",
                  selected && "opacity-100",
                )}
              >
                <HugeiconsIcon aria-hidden="true" icon={MoreVerticalIcon} className="size-3.5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-fit min-w-0 max-w-64">
              <DropdownMenuItem
                className="items-start text-destructive data-highlighted:bg-destructive/10 data-highlighted:text-destructive"
                disabled={deleteDisabled}
                onSelect={() => requestDelete(buttonRef.current)}
              >
                {deleteLabel}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </li>
      </ContextMenuTrigger>
      <ContextMenuContent className="w-fit min-w-0 max-w-64">
        <ContextMenuItem
          variant="destructive"
          className="items-start"
          disabled={deleteDisabled}
          onSelect={() => requestDelete(buttonRef.current)}
        >
          {deleteLabel}
        </ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  );
});

function DayStat({ value, unit }: { value: number; unit: string }) {
  return (
    <span className="truncate">
      <span className="font-semibold tabular-nums text-foreground">{value}</span> {unit}
    </span>
  );
}

function DeleteLabel({ hint, children }: { hint: string | null; children: ReactNode }) {
  return (
    <>
      <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="mt-0.5 size-4" />
      <span className="grid gap-0.5">
        <span>{children}</span>
        {hint ? <span className="text-xs text-muted-foreground">{hint}</span> : null}
      </span>
    </>
  );
}
