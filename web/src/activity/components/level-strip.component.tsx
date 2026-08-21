import { Alert02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/ui/tooltip.component";
import { cn } from "@/ui/ui-class.utils";
import type { ActivityLevelCardOverview, LevelMetadata } from "../activity.model";
import { formatTime } from "../lib/activity-date.utils";

export function LevelStrip({
  levelSessions,
  selectedLevelGroupId,
  timeZone,
  metadataFor,
  onSelectLevelGroup,
}: {
  levelSessions: ActivityLevelCardOverview[];
  selectedLevelGroupId: string | null;
  timeZone: string;
  metadataFor: (session: ActivityLevelCardOverview) => LevelMetadata;
  onSelectLevelGroup: (id: string) => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  return (
    <div className="border-b border-border bg-muted/10 px-3 py-2">
      <TooltipProvider>
        <div className="flex gap-2 overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
          {levelSessions.map((session) => {
            const metadata = metadataFor(session);
            const artist =
              metadata.artist ||
              (metadata.levelId === null ? t("metadata.localLevel") : t("metadata.tufDatabase"));
            const name =
              metadata.name ||
              (metadata.levelId === null
                ? t("metadata.customLevel")
                : t("metadata.levelNumber", { levelId: metadata.levelId }));
            const creator = metadata.creator || t("metadata.unknownCreator");
            const difficulty =
              metadata.difficulty ||
              (metadata.levelId === null ? t("metadata.local") : t("metadata.unknownDifficulty"));
            const hiddenRunsWarning = t("levels.hiddenRuns", {
              count: session.HiddenRunCount,
            });
            const unavailableRunsWarning = t("levels.unavailableRuns", {
              count: session.HiddenRunCount,
            });
            const warning = session.CanOpen ? hiddenRunsWarning : unavailableRunsWarning;
            const labelId = `level-card-${session.Id}`;
            const card = (
              <div
                key={session.Id}
                aria-disabled={session.CanOpen ? undefined : true}
                aria-label={session.CanOpen ? undefined : unavailableRunsWarning}
                tabIndex={session.CanOpen ? undefined : 0}
                className={cn(
                  "relative grid w-fit min-w-[22rem] max-w-[26rem] flex-none grid-cols-[3.5rem_minmax(0,1fr)] items-center gap-3 overflow-hidden rounded-md border border-border bg-background/60 px-3 py-3 text-left transition",
                  !session.CanOpen &&
                    "border-amber-500/30 opacity-40 transition-[border-color,opacity] duration-300 ease-out before:pointer-events-none before:absolute before:inset-0 before:z-0 before:bg-[repeating-linear-gradient(-45deg,rgba(245,158,11,0.18)_0_4px,transparent_4px_14px)] before:opacity-0 before:transition-opacity before:duration-300 before:ease-out before:content-[''] hover:border-amber-500/50 hover:before:opacity-100",
                  session.CanOpen &&
                    selectedLevelGroupId === session.LevelGroupId &&
                    "border-primary/60 bg-primary/10 ring-1 ring-primary/30",
                )}
              >
                <button
                  type="button"
                  aria-labelledby={labelId}
                  disabled={!session.CanOpen}
                  onClick={() => onSelectLevelGroup(session.LevelGroupId)}
                  className={cn(
                    "absolute inset-0 z-0 rounded-md transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring disabled:cursor-not-allowed",
                    session.CanOpen && "hover:bg-muted/50",
                  )}
                />
                <div className="pointer-events-none relative z-10 flex size-14 shrink-0 items-center justify-center">
                  {metadata.difficultyIconUrl ? (
                    <img
                      src={metadata.difficultyIconUrl}
                      alt={difficulty}
                      className="size-full object-contain drop-shadow-[0_3px_6px_rgb(0_0_0/0.75)]"
                      loading="lazy"
                      decoding="async"
                    />
                  ) : (
                    <div className="grid size-full place-items-center rounded-full bg-primary/15 text-xs font-semibold ring-1 ring-primary/50">
                      {difficulty}
                    </div>
                  )}
                </div>
                <div id={labelId} className="pointer-events-none relative z-10 min-w-0">
                  <p className="truncate text-xs text-muted-foreground">
                    {metadata.levelId === null
                      ? metadata.artist
                        ? t("metadata.localArtist", { artist })
                        : t("metadata.localUnknown")
                      : t("metadata.tufArtist", {
                          levelId: metadata.levelId,
                          artist,
                        })}
                  </p>
                  <div className="truncate font-heading text-lg font-semibold">{name}</div>
                  <p className="truncate text-xs text-muted-foreground" title={creator}>
                    {t("metadata.chartBy", { creator })}
                  </p>
                  <div className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground">
                    <span>
                      {session.RunCount} {t("counts.runs", { count: session.RunCount })}
                    </span>
                    {session.HiddenRunCount > 0 && session.CanOpen ? (
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <button
                            type="button"
                            aria-label={warning}
                            className="pointer-events-auto relative z-20 grid size-5 place-items-center rounded-sm text-amber-500 transition-colors hover:bg-amber-500/10 hover:text-amber-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500/70"
                          >
                            <HugeiconsIcon icon={Alert02Icon} size={14} strokeWidth={2} />
                          </button>
                        </TooltipTrigger>
                        <TooltipContent side="bottom">{warning}</TooltipContent>
                      </Tooltip>
                    ) : session.HiddenRunCount > 0 ? (
                      <span
                        aria-hidden="true"
                        className="relative z-20 grid size-5 place-items-center text-amber-500"
                      >
                        <HugeiconsIcon icon={Alert02Icon} size={14} strokeWidth={2} />
                      </span>
                    ) : null}
                    <span>·</span>
                    <span>
                      {session.ClearRunCount} {t("counts.clears", { count: session.ClearRunCount })}
                    </span>
                    <span>·</span>
                    <span>
                      {formatTime(session.LastSeenAtUtc, locale, t("time.open"), timeZone)}
                    </span>
                  </div>
                </div>
              </div>
            );
            return session.CanOpen ? (
              card
            ) : (
              <Tooltip key={session.Id} delayDuration={0} disableHoverableContent>
                <TooltipTrigger asChild>{card}</TooltipTrigger>
                <TooltipContent side="bottom">{unavailableRunsWarning}</TooltipContent>
              </Tooltip>
            );
          })}
        </div>
      </TooltipProvider>
    </div>
  );
}
