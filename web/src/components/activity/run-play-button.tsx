import { Loading03Icon, PlayIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { useTransientFlag } from "@/hooks/shared/use-transient-flag";
import type { ActivityRun } from "@/models/activity/activity-model";
import { replayButtonState } from "@/models/activity/replay-button-state";
import {
  describeRunReplay,
  type RunReplayContext,
  replayBusy,
  runReplayPhase,
} from "@/models/replay/run-replay-presentation";
import { cn } from "@/shared/lib/cn";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

function useRunPlayState(run: ActivityRun, context: RunReplayContext) {
  const { t } = useTranslation("activity");
  const { t: replayT } = useTranslation("replay");
  const phase = runReplayPhase(run, context);
  const state = replayButtonState(run, context.readOnly, replayBusy(context));
  const message = describeRunReplay(run, context, t, replayT);
  const failureKey =
    phase === "failed"
      ? context.error || context.status.message || context.status.errorCode || "failed"
      : "";
  const failureVisible = useTransientFlag(failureKey, 5_000);
  return { phase, state, message, failureVisible };
}

/** Round play control for a table row; the tooltip explains anything that blocks it. */
export function RunPlayIconButton({
  run,
  context,
  onPlay,
}: {
  run: ActivityRun;
  context: RunReplayContext;
  onPlay: (run: ActivityRun) => void;
}) {
  const { t } = useTranslation("activity");
  const { phase, state, message, failureVisible } = useRunPlayState(run, context);
  const label = `${t("run.playReplayForRun", { runIndex: run.runIndex })}${message ? `. ${message}` : ""}`;
  const starting = phase === "starting";
  const playing = phase === "playing";

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span className="inline-flex" tabIndex={state.tooltipTabIndex}>
          <button
            type="button"
            data-attempt-control
            aria-label={label}
            aria-disabled={state.ariaDisabled}
            disabled={state.disabled}
            onClick={(event) => {
              event.stopPropagation();
              if (!state.ariaDisabled) onPlay(run);
            }}
            onDoubleClick={(event) => event.stopPropagation()}
            className={cn(
              "grid size-8 place-items-center rounded-full bg-primary/12 text-primary ring-1 ring-primary/25 transition-[background-color,color,box-shadow,transform,opacity] duration-150 hover:bg-primary hover:text-primary-foreground active:scale-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring motion-reduce:active:scale-100",
              state.cursor === "not-allowed" && "disabled:cursor-not-allowed",
              state.cursor === "default" && "disabled:cursor-default",
              state.cursor === "wait" && "disabled:cursor-wait",
              "disabled:bg-foreground/5 disabled:text-subtle-foreground disabled:ring-foreground/10 disabled:hover:bg-foreground/5 disabled:hover:text-subtle-foreground",
              (starting || playing) &&
                "disabled:bg-primary disabled:text-primary-foreground disabled:ring-primary disabled:hover:bg-primary disabled:hover:text-primary-foreground",
              failureVisible &&
                "bg-destructive/12 text-destructive ring-destructive/40 hover:bg-destructive hover:text-white",
            )}
          >
            {starting ? (
              <HugeiconsIcon
                aria-hidden="true"
                icon={Loading03Icon}
                className="size-4 animate-spin motion-reduce:animate-none"
                strokeWidth={2.2}
              />
            ) : playing ? (
              <PlayingBars />
            ) : (
              <HugeiconsIcon
                aria-hidden="true"
                icon={PlayIcon}
                className="size-3.5 translate-x-px"
                fill="currentColor"
                strokeWidth={0}
              />
            )}
          </button>
        </span>
      </TooltipTrigger>
      <TooltipContent side="right" className="max-w-80 leading-relaxed">
        {message || t("run.playReplay")}
      </TooltipContent>
    </Tooltip>
  );
}

function PlayingBars() {
  return (
    <span aria-hidden="true" className="flex h-3.5 items-end gap-[2px]">
      {[0, 1, 2].map((bar) => (
        <span
          key={bar}
          className="w-[3px] origin-bottom rounded-full bg-current motion-safe:animate-[playing-bar_900ms_ease-in-out_infinite]"
          style={{ height: "100%", animationDelay: `${bar * 150}ms` }}
        />
      ))}
    </span>
  );
}
