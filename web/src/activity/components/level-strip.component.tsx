import type { ActivityLevelSessionOverview, LevelMetadata } from "../activity.model";
import { formatTime } from "../lib/activity-date.utils";
import { cn } from "@/ui/ui-class.utils";

export function LevelStrip({ levelSessions, selectedLevelSessionId, timeZone, metadataFor, onSelectLevelSession }: {
  levelSessions: ActivityLevelSessionOverview[];
  selectedLevelSessionId: string | null;
  timeZone: string;
  metadataFor: (levelId: number | null) => LevelMetadata;
  onSelectLevelSession: (id: string) => void;
}) {
  return <div className="border-b border-border bg-muted/10 px-3 py-2"><div className="flex gap-2 overflow-x-auto">
    {levelSessions.map((session) => { const metadata = metadataFor(session.TufLevelId); return (
      <button key={session.Id} type="button" onClick={() => onSelectLevelSession(session.Id)} className={cn("grid min-w-[30rem] grid-cols-[3.5rem_minmax(0,1fr)] items-center gap-3 rounded-md border border-border bg-background/60 px-3 py-3 text-left transition hover:bg-muted/50", selectedLevelSessionId === session.Id && "border-primary/60 bg-primary/10 ring-1 ring-primary/30")}>
        <div className="grid size-12 place-items-center rounded-full bg-primary/15 text-xs font-semibold ring-1 ring-primary/50">{metadata.difficulty}</div>
        <div className="min-w-0"><p className="truncate text-xs text-muted-foreground">{metadata.levelId === null ? "Local / unknown metadata" : `#${metadata.levelId} · ${metadata.artist}`}</p><div className="truncate font-heading text-lg font-semibold">{metadata.name}</div><div className="mt-1 flex gap-1.5 text-xs text-muted-foreground"><span>{session.RunCount} runs</span><span>·</span><span>{session.ClearRunCount} clears</span><span>·</span><span>{formatTime(session.OpenedAtUtc, timeZone)}</span></div></div>
      </button>
    ); })}
  </div></div>;
}
