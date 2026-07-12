import type { ActivityRun } from "../activity.model";
import { isFullClear } from "../lib/activity-selectors";
import { formatTime } from "../lib/activity-date.utils";
import { formatNumber } from "@/shared/lib/format.utils";
import { Badge } from "@/ui/badge.component";

function RunStatusBadge({ run }: { run: ActivityRun }) {
  if (isFullClear(run)) return <Badge variant="default">clear</Badge>;
  if (run.NoFailMode) return <Badge variant="secondary">no-fail</Badge>;
  return null;
}

export function RunTooltip({
  run,
  pinned,
  style,
  onClose,
}: {
  run: ActivityRun;
  pinned: boolean;
  style: { left: number; top: number };
  onClose: () => void;
}) {
  return (
    <div
      className="fixed z-50 w-64 rounded-lg border border-border bg-popover p-3 text-sm text-popover-foreground shadow-xl"
      style={style}
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <div className="font-heading text-base font-semibold">Run #{run.RunIndex}</div>
            <RunStatusBadge run={run} />
          </div>
          <div className="mt-0.5 text-xs text-muted-foreground">
            {formatTime(run.StartedAtUtc)} · group {run.SegmentGroupIndex}
          </div>
        </div>
        {pinned ? (
          <button
            type="button"
            className="rounded-md px-1.5 text-xs text-muted-foreground hover:bg-muted hover:text-foreground"
            onClick={onClose}
          >
            ×
          </button>
        ) : null}
      </div>
      <div className="mt-3 grid grid-cols-2 gap-2">
        <TooltipStat label="Tiles" value={`${run.StartTile} → ${run.LastTile}`} />
        <TooltipStat label="Pitch" value={`${run.LevelPitchPercent}%`} />
        <TooltipStat label="Hits" value={formatNumber(run.HitContextCount)} />
        <TooltipStat label="Inputs" value={formatNumber(run.InputCount)} />
      </div>
    </div>
  );
}

function TooltipStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md bg-muted/40 px-2 py-1.5">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
    </div>
  );
}

