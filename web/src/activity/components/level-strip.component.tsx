import type { ActivityLevelSession, LevelMetadata } from "../activity.model";
import { getLevelMetadata, getLevelSessionClearCount } from "../lib/activity-selectors";
import { formatTime } from "../lib/activity-date.utils";
import { cn } from "@/ui/ui-class.utils";

export function LevelStrip({
  levelSessions,
  selectedLevelSessionId,
  onSelectLevelSession,
}: {
  levelSessions: ActivityLevelSession[];
  selectedLevelSessionId: string;
  onSelectLevelSession: (id: string) => void;
}) {
  return (
    <div className="border-b border-border bg-muted/10 px-3 py-2">
      <div className="flex gap-2 overflow-x-auto">
        {levelSessions.map((session) => {
          const metadata = getLevelMetadata(session.TufLevelId);
          const clearCount = getLevelSessionClearCount(session);

          return (
            <button
              key={session.Id}
              type="button"
              className={cn(
                "grid min-w-[34rem] grid-cols-[4.25rem_minmax(0,1fr)_minmax(7rem,0.45fr)] items-center gap-3 rounded-md border border-border bg-background/60 px-3 py-3 text-left transition hover:bg-muted/50",
                selectedLevelSessionId === session.Id && "border-primary/60 bg-primary/10 ring-1 ring-primary/30",
              )}
              onClick={() => onSelectLevelSession(session.Id)}
            >
              <DifficultyIcon metadata={metadata} />
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-muted-foreground">
                  #{metadata.LevelId} - {metadata.Artist}
                </p>
                <div className="truncate font-heading text-xl font-semibold leading-tight">{metadata.Name}</div>
                <div className="mt-2 flex min-w-0 flex-wrap items-center gap-1.5">
                  <LevelStatPill value={`${session.RunCount} runs`} />
                  <LevelStatPill value={`${clearCount} clears`} />
                  <LevelStatPill value={formatTime(session.OpenedAtUtc)} />
                </div>
              </div>
              <div className="min-w-0">
                <p className="text-xs font-medium text-muted-foreground">Creator</p>
                <p className="truncate font-heading text-lg font-semibold leading-tight">{metadata.Creator}</p>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}

function DifficultyIcon({ metadata }: { metadata: LevelMetadata }) {
  if (metadata.DifficultyIconUrl) {
    return (
      <div className="flex size-14 shrink-0 items-center justify-center">
        <img
          src={metadata.DifficultyIconUrl}
          alt={metadata.Difficulty}
          className="size-full object-contain drop-shadow-[0_3px_8px_rgb(0_0_0/0.65)]"
          loading="lazy"
          decoding="async"
        />
      </div>
    );
  }

  return (
    <div className="flex size-14 shrink-0 items-center justify-center rounded-full bg-primary/15 font-heading text-sm font-semibold ring-2 ring-primary/50">
      {metadata.Difficulty}
    </div>
  );
}

function LevelStatPill({ value }: { value: string }) {
  return (
    <span className="rounded-md border border-border bg-muted/40 px-1.5 py-0.5 text-xs font-medium text-muted-foreground">
      {value}
    </span>
  );
}

