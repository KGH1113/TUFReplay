import type { ConnectionStatus } from "../activity.model";
import { Badge } from "@/ui/badge.component";
import { Button } from "@/ui/button.component";

export function DashboardHeader({ status, timeZone, timeZones, onTimeZoneChange, onRetry }: {
  status: ConnectionStatus;
  timeZone: string;
  timeZones: string[];
  onTimeZoneChange: (timeZone: string) => void;
  onRetry: () => void;
}) {
  return (
    <header className="flex min-h-14 items-center justify-between gap-3 border-b border-border bg-background px-4 py-2">
      <div><h1 className="font-heading text-xl font-semibold">TUFReplay</h1><p className="text-xs text-muted-foreground">Activity explorer</p></div>
      <div className="flex items-center gap-2">
        <label className="text-xs text-muted-foreground">Display timezone</label>
        <select className="h-8 max-w-64 rounded-md border border-border bg-background px-2 text-sm" value={timeZone} onChange={(event) => onTimeZoneChange(event.target.value)}>
          {timeZones.map((zone) => <option key={zone} value={zone}>{zone}</option>)}
        </select>
        <Badge variant="outline" className={status === "online" ? "border-[#7DCF00] text-[#7DCF00]" : ""}>{status}</Badge>
        {status === "error" ? <Button size="sm" onClick={onRetry}>Retry</Button> : null}
      </div>
    </header>
  );
}
