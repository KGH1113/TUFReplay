import { Alert02Icon, ArrowUpRight01Icon, Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { LevelDifficultyBadge } from "@/components/activity/level-difficulty-badge";
import { RelativeLevelPath } from "@/components/activity/relative-level-path";
import type { LevelRunSummary } from "@/models/activity/activity-data";
import { formatTime } from "@/models/activity/activity-date";
import type { LevelCard, LevelMetadata } from "@/models/activity/activity-model";
import { describeLevel } from "@/models/activity/level-description";
import { cn } from "@/shared/lib/cn";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

/** Who made the selected level, where it lives, and how today's attempts went. */
export function LevelHeader({
  level,
  metadata,
  summary,
  timeZone,
  leading,
}: {
  level: LevelCard;
  metadata: LevelMetadata;
  summary: LevelRunSummary;
  timeZone: string;
  /** Slot before the title, used for the sidebar toggle on narrow windows. */
  leading?: ReactNode;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  const text = describeLevel(metadata, t);
  const clears = Math.max(level.clearRunCount, summary.clearCount);

  return (
    <header className="flex min-w-0 shrink-0 items-center gap-4 px-5 pt-4 pb-3">
      {leading}
      <LevelDifficultyBadge metadata={metadata} label={text.difficulty} size="lg" />
      <div className="min-w-0 flex-1">
        <div className="flex min-w-0 items-center gap-1.5 text-xs text-muted-foreground">
          {metadata.levelId === null ? (
            <span className="truncate">{text.source}</span>
          ) : (
            <>
              <Tooltip>
                <TooltipTrigger asChild>
                  <a
                    href={`https://tuforums.com/levels/${metadata.levelId}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    aria-label={t("metadata.openTufLevelLabel", { levelId: metadata.levelId })}
                    className="-mx-1 inline-flex shrink-0 items-center gap-0.5 rounded-sm px-1 py-0.5 font-medium text-foreground/80 transition-colors hover:bg-foreground/8 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    {t("metadata.tufLevelLink", { levelId: metadata.levelId })}
                    <HugeiconsIcon
                      aria-hidden="true"
                      icon={ArrowUpRight01Icon}
                      size={12}
                      strokeWidth={2.2}
                    />
                  </a>
                </TooltipTrigger>
                <TooltipContent side="bottom">{t("metadata.openTufLevel")}</TooltipContent>
              </Tooltip>
              <span aria-hidden="true">·</span>
              <span className="truncate">{text.artist}</span>
            </>
          )}
        </div>
        <h1 className="truncate text-[22px] leading-tight font-semibold tracking-[-0.015em]">
          {text.name}
        </h1>
        <p className="flex min-w-0 items-center gap-1.5 text-xs text-muted-foreground">
          <span className="shrink-0">{t("metadata.chartBy", { creator: text.creator })}</span>
          {level.relativeLevelPath ? (
            <>
              <span aria-hidden="true">·</span>
              <span className="min-w-0 flex-1 font-mono text-[11px]">
                <RelativeLevelPath key={level.relativeLevelPath} path={level.relativeLevelPath} />
              </span>
            </>
          ) : null}
        </p>
        <p className="mt-1 text-xs tabular-nums text-muted-foreground sm:hidden">
          {[
            `${level.runCount} ${t("counts.runs", { count: level.runCount })}`,
            `${clears} ${t("counts.clears", { count: clears })}`,
            `${t("header.best")} ${
              summary.bestFromStartPercent === null ? "—" : `${summary.bestFromStartPercent}%`
            }`,
            formatTime(level.lastSeenAtUtc, locale, t("time.open"), timeZone),
          ].join(" · ")}
          {level.hiddenRunCount > 0
            ? ` · ${t("levels.hiddenRuns", { count: level.hiddenRunCount })}`
            : ""}
        </p>
      </div>
      <dl className="hidden shrink-0 items-stretch gap-1 sm:flex">
        <HeaderStat label={t("header.attempts")}>
          <span className="inline-flex items-center gap-1">
            {level.runCount}
            {level.hiddenRunCount > 0 ? (
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    type="button"
                    aria-label={t("levels.hiddenRuns", { count: level.hiddenRunCount })}
                    className="grid size-5 place-items-center rounded-sm text-warning transition-colors hover:bg-warning/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-warning/60"
                  >
                    <HugeiconsIcon
                      aria-hidden="true"
                      icon={Alert02Icon}
                      size={14}
                      strokeWidth={2}
                    />
                  </button>
                </TooltipTrigger>
                <TooltipContent side="bottom" className="max-w-64">
                  {t("levels.hiddenRuns", { count: level.hiddenRunCount })}
                </TooltipContent>
              </Tooltip>
            ) : null}
          </span>
        </HeaderStat>
        <HeaderStat label={t("header.clears")} accent={clears > 0}>
          <span className="inline-flex items-center gap-1">
            {clears > 0 ? (
              <HugeiconsIcon aria-hidden="true" icon={Tick02Icon} size={15} strokeWidth={2.6} />
            ) : null}
            {clears}
          </span>
        </HeaderStat>
        <HeaderStat label={t("header.best")}>
          {summary.bestFromStartPercent === null ? "—" : `${summary.bestFromStartPercent}%`}
        </HeaderStat>
        <HeaderStat label={t("header.lastPlayed")}>
          {formatTime(level.lastSeenAtUtc, locale, t("time.open"), timeZone)}
        </HeaderStat>
      </dl>
    </header>
  );
}

function HeaderStat({
  label,
  accent = false,
  children,
}: {
  label: string;
  accent?: boolean;
  children: ReactNode;
}) {
  return (
    <div className="min-w-[4.5rem] rounded-xl px-3 py-1.5 text-right">
      <dt className="text-[11px] font-medium text-subtle-foreground">{label}</dt>
      <dd
        className={cn(
          "mt-0.5 text-base font-semibold tabular-nums tracking-tight",
          accent && "text-primary",
        )}
      >
        {children}
      </dd>
    </div>
  );
}
