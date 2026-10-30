import {
  Alert02Icon,
  Delete02Icon,
  MoreHorizontalIcon,
  SquareLock02Icon,
  Tick02Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { memo, type ReactNode, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { LevelDifficultyBadge } from "@/components/activity/level-difficulty-badge";
import { dayStampParts, formatTime } from "@/models/activity/activity-date";
import type { ActivityDay, LevelCard, LevelMetadata } from "@/models/activity/activity-model";
import { dayDeleteBlocker } from "@/models/activity/day-deletion";
import { describeLevel } from "@/models/activity/level-description";
import { cn } from "@/shared/lib/cn";
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
import { HoverCard, HoverCardContent, HoverCardTrigger } from "@/shared/ui/hover-card";

export const DayListRow = memo(function DayListRow({
  day,
  selected,
  expanded,
  today,
  readOnly,
  selectedLevelGroupId,
  timeZone,
  metadataFor,
  registerButton,
  onToggle,
  onSelectLevel,
  onRequestDelete,
}: {
  day: ActivityDay;
  /** The selected level belongs to this day. */
  selected: boolean;
  expanded: boolean;
  today: boolean;
  readOnly: boolean;
  selectedLevelGroupId: string | null;
  timeZone: string;
  metadataFor: (level: LevelCard) => LevelMetadata;
  registerButton: (date: string, node: HTMLButtonElement | null) => void;
  onToggle: (date: string) => void;
  onSelectLevel: (date: string, levelGroupId: string) => void;
  onRequestDelete: (day: ActivityDay, returnFocus: HTMLElement | null) => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const stamp = dayStampParts(day.date, locale);
  const blocker = dayDeleteBlocker(day);
  const deleteDisabled = readOnly || blocker !== null;
  const levelCount = day.levelSessions.length;
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  // Mount the level list on first open so collapsed days cost nothing until they are used.
  const [rendered, setRendered] = useState(expanded);
  if (expanded && !rendered) setRendered(true);
  const label = [
    `${stamp.fullLabel}${today ? `, ${t("dayRail.today")}` : ""}`,
    `${day.runCount} ${t("counts.runs", { count: day.runCount })}`,
    `${levelCount} ${t("counts.levels", { count: levelCount })}`,
    `${day.clearRunCount} ${t("counts.clears", { count: day.clearRunCount })}`,
    day.lockedRunCount ? t("dayRail.locked", { count: day.lockedRunCount }) : "",
  ]
    .filter(Boolean)
    .join(", ");
  const requestDelete = (returnFocus: HTMLElement | null) => {
    if (!deleteDisabled) onRequestDelete(day, returnFocus);
  };
  const deleteLabel = (
    <DeleteLabel hint={blocker ? t(`dayDelete.blocked.${blocker}`) : null}>
      {t("dayDelete.action")}
    </DeleteLabel>
  );

  return (
    <li className="relative min-w-0">
      <ContextMenu>
        <ContextMenuTrigger asChild disabled={readOnly}>
          <div className="group/day relative">
            <button
              ref={(node) => {
                buttonRef.current = node;
                registerButton(day.date, node);
              }}
              type="button"
              aria-current={selected ? "date" : undefined}
              aria-expanded={expanded}
              aria-label={label}
              onClick={() => onToggle(day.date)}
              onKeyDown={(event) => {
                if (event.key !== "Delete" && event.key !== "Backspace") return;
                event.preventDefault();
                requestDelete(event.currentTarget);
              }}
              className={cn(
                "grid w-full grid-cols-[2.5rem_minmax(0,1fr)] items-center gap-3 rounded-xl py-2 pr-9 pl-2 text-left transition-[background-color,transform] duration-150 hover:bg-sidebar-accent active:scale-[0.985] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-sidebar-ring motion-reduce:active:scale-100",
                selected && "bg-sidebar-accent",
              )}
            >
              <span
                aria-hidden="true"
                className={cn(
                  "grid h-10 place-items-center content-center rounded-lg leading-none",
                  today ? "bg-primary text-primary-foreground" : "bg-foreground/[0.05]",
                )}
              >
                <span
                  className={cn(
                    "text-[10px] font-semibold uppercase tracking-[0.06em]",
                    today ? "text-primary-foreground/75" : "text-muted-foreground",
                  )}
                >
                  {stamp.weekday}
                </span>
                <span className="mt-0.5 text-[17px] font-semibold tabular-nums tracking-tight">
                  {stamp.day}
                </span>
              </span>
              <span aria-hidden="true" className="grid min-w-0 gap-0.5 text-xs">
                <span className="truncate text-muted-foreground">
                  {today ? (
                    <span className="mr-1.5 font-semibold text-foreground">
                      {t("dayRail.today")}
                    </span>
                  ) : null}
                  <span className="font-medium tabular-nums text-foreground">{day.runCount}</span>{" "}
                  {t("counts.runs", { count: day.runCount })}
                  <span className="text-subtle-foreground"> · </span>
                  <span className="tabular-nums">{levelCount}</span>{" "}
                  {t("counts.levels", { count: levelCount })}
                </span>
                <span className="flex min-w-0 items-center gap-2">
                  <span
                    className={cn(
                      "inline-flex items-center gap-0.5 truncate",
                      day.clearRunCount > 0
                        ? "font-semibold text-primary"
                        : "text-subtle-foreground",
                    )}
                  >
                    {day.clearRunCount > 0 ? (
                      <HugeiconsIcon icon={Tick02Icon} size={12} strokeWidth={2.6} />
                    ) : null}
                    <span className="tabular-nums">{day.clearRunCount}</span>{" "}
                    {t("counts.clears", { count: day.clearRunCount })}
                  </span>
                  {day.lockedRunCount > 0 ? (
                    <span className="inline-flex shrink-0 items-center gap-0.5 tabular-nums text-muted-foreground">
                      <HugeiconsIcon icon={SquareLock02Icon} size={12} strokeWidth={2} />
                      {day.lockedRunCount}
                    </span>
                  ) : null}
                </span>
              </span>
            </button>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button
                  type="button"
                  aria-label={t("dayRail.openActions", { date: stamp.fullLabel })}
                  className={cn(
                    "absolute top-1/2 right-1.5 grid size-7 -translate-y-1/2 place-items-center rounded-md text-muted-foreground opacity-0 transition-[opacity,background-color] hover:bg-foreground/8 hover:text-foreground focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring group-hover/day:opacity-100 data-[state=open]:opacity-100 [@media(hover:none)]:opacity-100",
                    selected && "opacity-100",
                  )}
                >
                  <HugeiconsIcon aria-hidden="true" icon={MoreHorizontalIcon} className="size-4" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" side="right" className="w-fit min-w-0 max-w-64">
                <DropdownMenuItem
                  className="items-start text-destructive data-highlighted:bg-destructive/10 data-highlighted:text-destructive"
                  disabled={deleteDisabled}
                  onSelect={() => requestDelete(buttonRef.current)}
                >
                  {deleteLabel}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
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
      {rendered && day.levelSessions.length > 0 ? (
        // Rows animate their height open and closed; the list stays mounted once opened.
        <div
          inert={!expanded}
          className={cn(
            "grid transition-[grid-template-rows,opacity] duration-300 ease-out-quint motion-reduce:transition-none",
            expanded
              ? "grid-rows-[1fr] opacity-100 starting:grid-rows-[0fr] starting:opacity-0"
              : "grid-rows-[0fr] opacity-0",
          )}
        >
          <div className="min-h-0 overflow-hidden">
            <ul
              aria-label={t("sidebar.levelsOf", { date: stamp.fullLabel })}
              className="mt-0.5 mb-1.5 grid min-w-0 grid-cols-1 gap-px pl-[3.25rem]"
            >
              {day.levelSessions.map((level) => (
                <LevelNavItem
                  key={level.id}
                  level={level}
                  metadata={metadataFor(level)}
                  selected={level.canOpen && level.levelGroupId === selectedLevelGroupId}
                  timeZone={timeZone}
                  onSelect={(levelGroupId) => onSelectLevel(day.date, levelGroupId)}
                />
              ))}
            </ul>
          </div>
        </div>
      ) : null}
    </li>
  );
});

function LevelNavItem({
  level,
  metadata,
  selected,
  timeZone,
  onSelect,
}: {
  level: LevelCard;
  metadata: LevelMetadata;
  selected: boolean;
  timeZone: string;
  onSelect: (levelGroupId: string) => void;
}) {
  const { t } = useTranslation("activity");
  const text = describeLevel(metadata, t);
  const unavailable = t("levels.unavailableRuns", { count: level.hiddenRunCount });
  const item = (
    <button
      type="button"
      aria-current={selected ? "page" : undefined}
      aria-disabled={!level.canOpen}
      aria-label={level.canOpen ? undefined : `${text.name}. ${unavailable}`}
      onClick={() => level.canOpen && onSelect(level.levelGroupId)}
      className={cn(
        "relative flex h-9 w-full min-w-0 items-center gap-2 rounded-lg pr-2 pl-2 text-left transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-sidebar-ring",
        level.canOpen ? "hover:bg-sidebar-accent" : "cursor-not-allowed",
        selected &&
          "bg-primary/10 text-foreground before:absolute before:inset-y-2 before:-left-0.5 before:w-[3px] before:rounded-full before:bg-primary",
      )}
    >
      <span className={cn("contents", !level.canOpen && "[&>*]:opacity-45")}>
        <LevelDifficultyBadge metadata={metadata} label={text.difficulty} size="sm" />
        <span className={cn("min-w-0 flex-1 truncate text-[13px]", selected && "font-semibold")}>
          {text.name}
        </span>
      </span>
      <span className="flex shrink-0 items-center gap-1.5 text-[11px] tabular-nums text-subtle-foreground">
        {level.hiddenRunCount > 0 ? (
          <HugeiconsIcon
            aria-hidden="true"
            icon={Alert02Icon}
            size={12}
            strokeWidth={2}
            className="text-warning"
          />
        ) : null}
        {level.runCount}
        {level.clearRunCount > 0 ? (
          <span className="inline-flex items-center font-semibold text-primary">
            <HugeiconsIcon aria-hidden="true" icon={Tick02Icon} size={11} strokeWidth={2.8} />
            {level.clearRunCount}
          </span>
        ) : null}
      </span>
    </button>
  );
  const warning = !level.canOpen
    ? unavailable
    : level.hiddenRunCount > 0
      ? t("levels.hiddenRuns", { count: level.hiddenRunCount })
      : null;
  return (
    <li className="min-w-0">
      <HoverCard openDelay={350} closeDelay={60}>
        <HoverCardTrigger asChild>{item}</HoverCardTrigger>
        <HoverCardContent
          side="right"
          align="start"
          sideOffset={10}
          className="w-72 space-y-3 p-3.5"
        >
          <LevelPreview level={level} metadata={metadata} timeZone={timeZone} />
          {warning ? (
            <p className="flex gap-2 rounded-lg bg-warning/10 p-2.5 text-xs leading-relaxed text-foreground/90">
              <HugeiconsIcon
                aria-hidden="true"
                icon={Alert02Icon}
                size={14}
                strokeWidth={2}
                className="mt-px shrink-0 text-warning"
              />
              {warning}
            </p>
          ) : null}
        </HoverCardContent>
      </HoverCard>
    </li>
  );
}

/** Level details shown when hovering a level in the sidebar. */
function LevelPreview({
  level,
  metadata,
  timeZone,
}: {
  level: LevelCard;
  metadata: LevelMetadata;
  timeZone: string;
}) {
  const { t, i18n } = useTranslation("activity");
  const text = describeLevel(metadata, t);
  const locale = i18n.resolvedLanguage ?? "en";
  return (
    <div className="flex gap-3">
      <LevelDifficultyBadge metadata={metadata} label={text.difficulty} size="lg" />
      <div className="min-w-0 flex-1 text-xs text-muted-foreground">
        <p className="truncate">
          {metadata.levelId === null
            ? text.source
            : `${t("metadata.tufLevelLink", { levelId: metadata.levelId })} · ${text.artist}`}
        </p>
        <p className="truncate text-sm font-semibold text-foreground">{text.name}</p>
        <p className="truncate">{t("metadata.chartBy", { creator: text.creator })}</p>
        {level.relativeLevelPath ? (
          <p className="truncate font-mono text-[11px]" title={level.relativeLevelPath}>
            {level.relativeLevelPath}
          </p>
        ) : null}
        <p className="mt-1 tabular-nums">
          {level.runCount} {t("counts.runs", { count: level.runCount })} · {level.clearRunCount}{" "}
          {t("counts.clears", { count: level.clearRunCount })} ·{" "}
          {formatTime(level.lastSeenAtUtc, locale, t("time.open"), timeZone)}
        </p>
      </div>
    </div>
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
