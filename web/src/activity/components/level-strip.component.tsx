import { cn } from "@/ui/ui-class.utils";
import type { ActivityLevelSessionOverview, LevelMetadata } from "../activity.model";
import { formatTime } from "../lib/activity-date.utils";

export function LevelStrip({
  levelSessions,
  selectedLevelSessionId,
  timeZone,
  metadataFor,
  onSelectLevelSession,
}: {
  levelSessions: ActivityLevelSessionOverview[];
  selectedLevelSessionId: string | null;
  timeZone: string;
  metadataFor: (levelId: number | null) => LevelMetadata;
  onSelectLevelSession: (id: string) => void;
}) {
  return (
    <div className="border-b border-border bg-muted/10 px-3 py-2">
      <div className="flex gap-2 overflow-x-auto">
        {levelSessions.map((session) => {
          const metadata = metadataFor(session.TufLevelId);
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
                    alt={metadata.difficulty}
                    className="size-full object-contain drop-shadow-[0_3px_6px_rgb(0_0_0/0.75)]"
                    loading="lazy"
                    decoding="async"
                  />
                ) : (
                  <div className="grid size-full place-items-center rounded-full bg-primary/15 text-xs font-semibold ring-1 ring-primary/50">
                    {metadata.difficulty}
                  </div>
                )}
              </div>
              <div className="min-w-0">
                <p className="truncate text-xs text-muted-foreground">
                  {metadata.levelId === null
                    ? "Local / unknown metadata"
                    : `#${metadata.levelId} · ${metadata.artist}`}
                </p>
                <div className="truncate font-heading text-lg font-semibold">{metadata.name}</div>
                <p className="truncate text-xs text-muted-foreground" title={metadata.creator}>
                  Chart by <span className="text-foreground/75">{metadata.creator}</span>
                </p>
                <div className="mt-1 flex gap-1.5 text-xs text-muted-foreground">
                  <span>{session.RunCount} runs</span>
                  <span>·</span>
                  <span>{session.ClearRunCount} clears</span>
                  <span>·</span>
                  <span>{formatTime(session.OpenedAtUtc, timeZone)}</span>
                </div>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
