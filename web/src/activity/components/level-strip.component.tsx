import { useTranslation } from "react-i18next";
import { cn } from "@/ui/ui-class.utils";
import type { ActivityLogicalLevelOverview, LevelMetadata } from "../activity.model";
import { formatTime } from "../lib/activity-date.utils";

export function LevelStrip({
  levelSessions,
  selectedLevelSessionId,
  timeZone,
  metadataFor,
  onSelectLevelSession,
}: {
  levelSessions: ActivityLogicalLevelOverview[];
  selectedLevelSessionId: string | null;
  timeZone: string;
  metadataFor: (session: ActivityLogicalLevelOverview) => LevelMetadata;
  onSelectLevelSession: (id: string) => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const locale = i18n.resolvedLanguage ?? "en";
  return (
    <div className="border-b border-border bg-muted/10 px-3 py-2">
      <div className="flex gap-2 overflow-x-auto">
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
          return (
            <button
              key={session.Id}
              type="button"
              onClick={() => onSelectLevelSession(session.Id)}
              className={cn(
                "grid w-fit min-w-[22rem] max-w-[26rem] flex-none grid-cols-[3.5rem_minmax(0,1fr)] items-center gap-3 rounded-md border border-border bg-background/60 px-3 py-3 text-left transition hover:bg-muted/50",
                selectedLevelSessionId === session.Id &&
                  "border-primary/60 bg-primary/10 ring-1 ring-primary/30",
              )}
            >
              <div className="flex size-14 shrink-0 items-center justify-center">
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
              <div className="min-w-0">
                <p className="truncate text-xs text-muted-foreground">
                  {metadata.levelId === null
                    ? metadata.artist
                      ? t("metadata.localArtist", { artist })
                      : t("metadata.localUnknown")
                    : t("metadata.tufArtist", { levelId: metadata.levelId, artist })}
                </p>
                <div className="truncate font-heading text-lg font-semibold">{name}</div>
                <p className="truncate text-xs text-muted-foreground" title={creator}>
                  {t("metadata.chartBy", { creator })}
                </p>
                <div className="mt-1 flex gap-1.5 text-xs text-muted-foreground">
                  <span>
                    {session.RunCount} {t("counts.runs", { count: session.RunCount })}
                  </span>
                  <span>·</span>
                  <span>
                    {session.ClearRunCount} {t("counts.clears", { count: session.ClearRunCount })}
                  </span>
                  <span>·</span>
                  <span>{formatTime(session.LastSeenAtUtc, locale, t("time.open"), timeZone)}</span>
                </div>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
