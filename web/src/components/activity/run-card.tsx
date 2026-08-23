import {
  ChartAverageIcon,
  Clock01Icon,
  DashboardSpeed01Icon,
  Loading03Icon,
  PercentIcon,
  PlayIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { TFunction } from "i18next";
import { memo, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { RunActionsMenu } from "@/components/activity/run-actions-menu";
import { RunDifficultyIcon } from "@/components/activity/run-difficulty-icon";
import { RunJudgmentStrip } from "@/components/activity/run-judgment-strip";
import { RunNoFailIcon } from "@/components/activity/run-no-fail-icon";
import { formatTimeWithOffsetParts } from "@/models/activity/activity-date";
import type { ActivityRun } from "@/models/activity/activity-model";
import { translatedDomainError } from "@/models/activity/localized-error";
import { formatXAccuracy } from "@/models/activity/x-accuracy";
import type { ReplayStatus } from "@/models/replay/replay-model";
import { cn } from "@/shared/lib/cn";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

export const RunCard = memo(function RunCard({
  run,
  active,
  readOnly,
  timeZone,
  replayStatus,
  replayPendingRunId,
  replayError,
  replayErrorRunId,
  onSelect,
  onPlayReplay,
  onDeleteRun,
  onKeepMicrophoneRecording,
  onDeleteMicrophoneRecording,
}: {
  run: ActivityRun;
  active: boolean;
  readOnly: boolean;
  timeZone: string;
  replayStatus: ReplayStatus;
  replayPendingRunId: string | null;
  replayError: string;
  replayErrorRunId: string | null;
  onSelect: (run: ActivityRun) => void;
  onPlayReplay: (run: ActivityRun) => void;
  onDeleteRun: (run: ActivityRun) => Promise<void>;
  onKeepMicrophoneRecording: (run: ActivityRun) => Promise<void>;
  onDeleteMicrophoneRecording: (run: ActivityRun) => Promise<void>;
}) {
  const { t } = useTranslation("activity");
  const statusMatches = replayStatus.runId === run.id;
  const runDeleteDisabled =
    replayPendingRunId === run.id ||
    (statusMatches &&
      (replayStatus.state === "preparing" ||
        replayStatus.state === "opening_level" ||
        replayStatus.state === "waiting_for_focus" ||
        replayStatus.state === "starting" ||
        replayStatus.state === "playing" ||
        replayStatus.state === "returning_to_editor"));

  return (
    <div
      data-run-id={run.id}
      className={cn(
        "group/run relative rounded-md border border-border bg-background/60 text-xs transition-colors hover:border-primary/60",
      )}
    >
      <div className="flex min-h-8 items-center gap-2 px-3 pt-2.5">
        <RunReplayButton
          run={run}
          status={replayStatus}
          pendingRunId={replayPendingRunId}
          error={replayError}
          errorRunId={replayErrorRunId}
          onPlay={onPlayReplay}
          disabled={readOnly}
        />
        <div className="ml-auto flex h-8 items-center gap-1">
          <RunDifficultyIcon difficulty={run.judgmentDifficulty} />
          <RunNoFailIcon enabled={run.noFailMode} />
          {run.hasMicrophoneRecording ? <MicrophoneRecordingIndicator /> : null}
        </div>
        <RunActionsMenu
          run={run}
          disabled={readOnly}
          runDeleteDisabled={runDeleteDisabled}
          onKeepMicrophoneRecording={onKeepMicrophoneRecording}
          onDeleteMicrophoneRecording={onDeleteMicrophoneRecording}
          onDeleteRun={onDeleteRun}
        />
      </div>

      <button
        type="button"
        aria-label={t("run.select", { runIndex: run.runIndex })}
        aria-pressed={active}
        onClick={() => onSelect(run)}
        className="block w-full rounded-b-md p-3 pt-2 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
      >
        <RunCardContent run={run} timeZone={timeZone} />
        <RunJudgmentStrip counts={run.judgmentCounts} />
      </button>
    </div>
  );
});

function MicrophoneRecordingIndicator() {
  const { t } = useTranslation("activity");
  const label = t("run.microphoneAvailable");
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          role="img"
          aria-label={label}
          className="-ml-1 grid size-6 shrink-0 place-items-center text-white"
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 24 24"
            className="size-4 -translate-y-[0.5px] overflow-visible"
            fill="none"
          >
            <rect x="8" y="2" width="8" height="13" rx="4" fill="currentColor" />
            <path
              d="M5.5 10.75v.5a6.5 6.5 0 0 0 13 0v-.5M12 17.75V22M9 22h6"
              stroke="currentColor"
              strokeWidth="2.25"
              strokeLinecap="round"
            />
          </svg>
        </span>
      </TooltipTrigger>
      <TooltipContent side="top">{label}</TooltipContent>
    </Tooltip>
  );
}

function RunCardContent({ run, timeZone }: { run: ActivityRun; timeZone: string }) {
  const { t, i18n } = useTranslation("activity");
  const startedAt = formatTimeWithOffsetParts(
    run.startedAtUtc,
    i18n.resolvedLanguage ?? "en",
    t("time.open"),
    timeZone,
  );

  return (
    <div className="grid grid-cols-2 gap-x-5 gap-y-2 rounded-md bg-muted/25 p-2.5">
      <ScoreboardMetric
        icon={PercentIcon}
        label={t("sort.progress")}
        value={`${runStartProgressPercent(run)}% → ${runProgressPercent(run)}%`}
      />
      <ScoreboardMetric
        icon={DashboardSpeed01Icon}
        label={t("sort.pitch")}
        value={`${run.levelPitchPercent ?? "?"}%`}
      />
      <ScoreboardMetric
        icon={ChartAverageIcon}
        label={t("run.xAccuracy")}
        value={formatXAccuracy(run.xAccuracy, i18n.resolvedLanguage ?? "en")}
      />
      <ScoreboardMetric
        icon={Clock01Icon}
        label={t("run.startedAt")}
        value={startedAt.time}
        suffix={startedAt.offset ? `(${startedAt.offset})` : undefined}
      />
    </div>
  );
}

function ScoreboardMetric({
  icon,
  label,
  value,
  suffix,
}: {
  icon: typeof PercentIcon;
  label: string;
  value: string;
  suffix?: string;
}) {
  return (
    <span
      className="flex min-w-0 items-center gap-2.5"
      title={`${label}: ${value}${suffix ? ` ${suffix}` : ""}`}
    >
      <span className="grid size-7 shrink-0 place-items-center rounded-sm bg-primary/10 text-primary">
        <HugeiconsIcon aria-hidden="true" icon={icon} size={15} strokeWidth={2} />
      </span>
      <span className="min-w-0">
        <span className="block text-[8px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">
          {label}
        </span>
        <span className="flex min-w-0 items-baseline gap-1 font-heading font-semibold leading-tight tabular-nums text-foreground">
          <span className="min-w-0 truncate text-[13px]">{value}</span>
          {suffix ? (
            <span className="shrink-0 text-[9px] font-medium text-muted-foreground">{suffix}</span>
          ) : null}
        </span>
      </span>
    </span>
  );
}

function RunReplayButton({
  run,
  status,
  pendingRunId,
  error,
  errorRunId,
  onPlay,
  disabled,
}: {
  run: ActivityRun;
  status: ReplayStatus;
  pendingRunId: string | null;
  error: string;
  errorRunId: string | null;
  onPlay: (run: ActivityRun) => void;
  disabled: boolean;
}) {
  const { t: activityT } = useTranslation("activity");
  const { t: replayT } = useTranslation("replay");
  const message = describeReplay(run.id, status, pendingRunId, error, errorRunId, replayT);
  const statusMatches = status.runId === run.id;
  const starting =
    pendingRunId === run.id ||
    (statusMatches &&
      (status.state === "preparing" ||
        status.state === "opening_level" ||
        status.state === "waiting_for_focus" ||
        status.state === "starting" ||
        status.state === "returning_to_editor"));
  const playing = statusMatches && status.state === "playing";
  const failed =
    (errorRunId === run.id && Boolean(error)) || (statusMatches && status.state === "error");
  const failureKey = failed ? error || status.message || status.errorCode || "replay-error" : "";
  const [failureVisible, setFailureVisible] = useState(false);
  useEffect(() => {
    if (!failureKey) {
      setFailureVisible(false);
      return;
    }
    setFailureVisible(true);
    const timeout = setTimeout(() => setFailureVisible(false), 5_000);
    return () => clearTimeout(timeout);
  }, [failureKey]);
  const label = `${activityT("run.playReplayForRun", { runIndex: run.runIndex })}${message ? `. ${message}` : ""}`;

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type="button"
          aria-label={label}
          aria-disabled={disabled || pendingRunId !== null}
          disabled={disabled}
          onClick={() => !disabled && pendingRunId === null && onPlay(run)}
          className={cn(
            "inline-flex h-8 items-center justify-center gap-1.5 rounded-md border border-primary/40 bg-primary/15 px-2 font-heading text-[11px] font-semibold tracking-wide text-primary shadow-[inset_0_1px_0_rgb(255_255_255/0.05)] transition-[color,background-color,border-color,box-shadow,transform,opacity] hover:border-primary/70 hover:bg-primary/25 hover:shadow-sm hover:shadow-primary/10 active:translate-y-px focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background aria-disabled:cursor-wait aria-disabled:opacity-45",
            "min-w-16",
            (starting || playing) && "border-primary/70 bg-primary/25",
            playing && "bg-primary text-primary-foreground shadow-sm shadow-primary/20",
            failureVisible &&
              "border-destructive/50 bg-destructive/10 text-destructive shadow-none hover:border-destructive/70 hover:bg-destructive/15",
          )}
        >
          <HugeiconsIcon
            aria-hidden="true"
            icon={starting ? Loading03Icon : PlayIcon}
            className={cn("size-4", starting && "animate-spin")}
            fill={starting ? "none" : "currentColor"}
            strokeWidth={starting ? 2.2 : 0}
          />
          <span>{playing ? activityT("run.playing") : activityT("run.play")}</span>
        </button>
      </TooltipTrigger>
      <TooltipContent side="top" align="end">
        {message || activityT("run.playReplay")}
      </TooltipContent>
    </Tooltip>
  );
}

function runStartProgressPercent(run: ActivityRun) {
  return tileProgressPercent(run.startTile, run.floorCount);
}

function runProgressPercent(run: ActivityRun) {
  const lastTile = Math.max(run.startTile, run.lastTile ?? run.startTile);
  return tileProgressPercent(lastTile, run.floorCount);
}

function tileProgressPercent(tile: number, floorCount: number) {
  return Math.round(Math.min(1, Math.max(0, tile) / Math.max(1, floorCount)) * 100);
}

function describeReplay(
  runId: string,
  status: ReplayStatus,
  pendingRunId: string | null,
  error: string,
  errorRunId: string | null,
  t: TFunction<"replay">,
) {
  if (pendingRunId === runId) return t("status.sending");
  if (errorRunId === runId && error) return error;
  if (status.runId === runId) {
    if (status.state === "preparing") return t("status.preparing");
    if (status.state === "opening_level") return t("status.openingLevel");
    if (status.state === "waiting_for_focus") return t("status.waitingForFocus");
    if (status.state === "starting") return t("status.starting");
    if (status.state === "playing") return t("status.playing");
    if (status.state === "returning_to_editor") return t("status.returning");
    if (status.state === "completed") return t("status.completed");
    if (status.state === "cancelled") return t("status.cancelled");
    if (status.state === "error")
      return (
        translatedDomainError(status.errorCode ?? "") ||
        status.message ||
        status.errorCode ||
        t("status.failed")
      );
    return null;
  }
  if (
    status.state === "preparing" ||
    status.state === "opening_level" ||
    status.state === "waiting_for_focus" ||
    status.state === "starting" ||
    status.state === "playing" ||
    status.state === "returning_to_editor"
  ) {
    return t("status.replaceActive");
  }
  return null;
}
