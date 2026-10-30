import { MoreHorizontalIcon, Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { type KeyboardEvent, memo } from "react";
import { useTranslation } from "react-i18next";
import { JudgmentStrip } from "@/components/activity/judgment-strip";
import { JudgmentTimeline } from "@/components/activity/judgment-timeline";
import { RunFlags } from "@/components/activity/run-flags";
import { RunLockButton } from "@/components/activity/run-lock-button";
import {
  type RunMenuHandlers,
  RunMenuItems,
  type RunMenuState,
} from "@/components/activity/run-menu";
import { RunPlayIconButton } from "@/components/activity/run-play-button";
import { formatTimeWithOffsetParts } from "@/models/activity/activity-date";
import type { ActivityRun } from "@/models/activity/activity-model";
import {
  formatDuration,
  runDurationMs,
  runEndPercent,
  runOutcome,
  runStartPercent,
} from "@/models/activity/run-progress";
import { formatXAccuracy } from "@/models/activity/x-accuracy";
import {
  type RunReplayContext,
  replayBusy,
  runReplayActive,
} from "@/models/replay/run-replay-presentation";
import { cn } from "@/shared/lib/cn";
import { ContextMenu, ContextMenuContent, ContextMenuTrigger } from "@/shared/ui/context-menu";
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger } from "@/shared/ui/dropdown-menu";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

export const AttemptRow = memo(function AttemptRow({
  run,
  offsetY,
  rowRef,
  selected,
  focusable,
  competitiveLayout,
  timeZone,
  replayContext,
  menuHandlers,
  onSelect,
  onSetLocked,
  onKeyDown,
}: {
  run: ActivityRun;
  /** Position inside the virtual list. */
  offsetY: number;
  rowRef?: (element: HTMLLIElement | null) => void;
  selected: boolean;
  /** The one row reachable with Tab; arrow keys move between rows. */
  focusable: boolean;
  /** The list uses competitive judgment columns, so classic Perfect spans its three. */
  competitiveLayout: boolean;
  timeZone: string;
  replayContext: RunReplayContext;
  menuHandlers: RunMenuHandlers;
  onSelect: (run: ActivityRun) => void;
  onSetLocked: (run: ActivityRun, locked: boolean) => Promise<void>;
  /** Arrow-key navigation and Enter to replay, shared by every row. */
  onKeyDown: (event: KeyboardEvent<HTMLLIElement>) => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const outcome = runOutcome(run);
  const clear = outcome === "clear";
  const startPercent = runStartPercent(run);
  const endPercent = runEndPercent(run);
  const started = formatTimeWithOffsetParts(run.startedAtUtc, locale, t("time.open"), timeZone);
  const duration = formatDuration(runDurationMs(run));
  const accuracy = formatXAccuracy(run.xAccuracy, locale);
  const menuState: RunMenuState = {
    readOnly: replayContext.readOnly,
    replayActive: runReplayActive(run, replayContext),
    replayBusy: replayBusy(replayContext),
  };

  return (
    <ContextMenu>
      <ContextMenuTrigger asChild>
        <li
          ref={rowRef}
          data-run-id={run.id}
          aria-current={selected ? "true" : undefined}
          style={{ transform: `translateY(${offsetY}px)` }}
          onKeyDown={onKeyDown}
          onClick={(event) => {
            // Menu items render in a portal but still bubble here through React.
            if (!event.currentTarget.contains(event.target as Node)) return;
            onSelect(run);
            // Keep keyboard focus on the clicked row so arrow keys continue from it.
            event.currentTarget
              .querySelector<HTMLElement>("[data-run-select]")
              ?.focus({ preventScroll: true });
          }}
          onDoubleClick={(event) => {
            if (!event.currentTarget.contains(event.target as Node)) return;
            if (!menuState.readOnly && run.replayPlayable && !menuState.replayBusy)
              menuHandlers.onPlay(run);
          }}
          className={cn(
            "attempt-grid group/attempt absolute inset-x-0 top-0 min-h-12 cursor-default border-b border-border/70 px-3 py-2 text-[13px] transition-colors duration-100 select-none",
            selected
              ? "bg-primary/[0.08] before:absolute before:inset-y-1.5 before:left-0 before:w-[3px] before:rounded-r-full before:bg-primary"
              : "hover:bg-foreground/[0.035]",
          )}
        >
          <div data-area="play">
            <RunPlayIconButton run={run} context={replayContext} onPlay={menuHandlers.onPlay} />
          </div>
          {/* Invisible full-row target: keyboard focus lands here and shows as a ring on the row. */}
          <button
            type="button"
            data-run-select={run.id}
            tabIndex={focusable ? 0 : -1}
            aria-pressed={selected}
            aria-label={t("run.select", { runIndex: run.runIndex })}
            onClick={(event) => {
              event.stopPropagation();
              onSelect(run);
            }}
            className="pointer-events-none absolute inset-0 rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset"
          />
          <div data-area="progress" className="flex min-w-0 items-center justify-end">
            <span
              className={cn(
                "w-[4.5rem] shrink-0 text-right font-semibold tabular-nums",
                clear ? "text-primary" : "text-foreground",
              )}
            >
              {clear ? (
                <span className="inline-flex items-center gap-1">
                  <HugeiconsIcon aria-hidden="true" icon={Tick02Icon} size={14} strokeWidth={2.6} />
                  {t("outcome.clear")}
                </span>
              ) : (
                <>
                  {startPercent > 0 ? (
                    <span className="font-normal text-muted-foreground">{startPercent}→</span>
                  ) : null}
                  {endPercent}%
                </>
              )}
            </span>
            <span className="sr-only">
              {t("attempts.progressLabel", { start: startPercent, end: endPercent })},{" "}
              {t(`outcome.${outcome}`)}
            </span>
          </div>
          <div data-area="acc" className="text-right font-semibold tabular-nums">
            <span className="sr-only">{t("run.xAccuracy")} </span>
            {accuracy}
          </div>
          <div data-area="pitch" className="text-right tabular-nums text-muted-foreground">
            <span className="sr-only">{t("sort.pitch")} </span>
            {run.levelPitchPercent ?? "?"}%
          </div>
          <div data-area="judg" className="min-w-0">
            <JudgmentStrip run={run} competitiveLayout={competitiveLayout} />
          </div>
          <div data-area="timeline" className="min-w-0">
            <JudgmentTimeline runId={run.id} enabled={!replayContext.readOnly} />
          </div>
          <div data-area="time" className="text-right tabular-nums text-muted-foreground">
            <Tooltip>
              <TooltipTrigger asChild>
                <span>{started.time}</span>
              </TooltipTrigger>
              <TooltipContent side="top">
                {t("attempts.startedTooltip", {
                  time: started.time,
                  offset: started.offset,
                })}
                {duration ? ` · ${t("attempts.durationTooltip", { duration })}` : ""}
              </TooltipContent>
            </Tooltip>
          </div>
          <div data-area="flags" className="flex min-w-0 justify-end">
            <RunFlags run={run} />
          </div>
          <div data-area="lock" className="flex justify-center">
            <RunLockButton
              run={run}
              disabled={menuState.readOnly}
              revealed={selected}
              onSetLocked={onSetLocked}
            />
          </div>
          <div data-area="menu" className="flex justify-center">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button
                  type="button"
                  data-attempt-control
                  aria-label={t("run.openActions", { runIndex: run.runIndex })}
                  onClick={(event) => event.stopPropagation()}
                  onDoubleClick={(event) => event.stopPropagation()}
                  className="grid size-7 place-items-center rounded-md text-subtle-foreground transition-colors hover:bg-foreground/8 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring data-[state=open]:bg-foreground/8 data-[state=open]:text-foreground"
                >
                  <HugeiconsIcon aria-hidden="true" icon={MoreHorizontalIcon} className="size-4" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-60">
                <RunMenuItems kind="dropdown" run={run} state={menuState} handlers={menuHandlers} />
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </li>
      </ContextMenuTrigger>
      <ContextMenuContent className="w-60">
        <RunMenuItems kind="context" run={run} state={menuState} handlers={menuHandlers} />
      </ContextMenuContent>
    </ContextMenu>
  );
});
