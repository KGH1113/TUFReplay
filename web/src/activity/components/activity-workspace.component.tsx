import { useCallback } from "react";

import type { ActivityChart, ActivityRun, RunMarker } from "../activity.model";
import { formatTime } from "../lib/activity-date.utils";
import { runsForMarker } from "../lib/activity-data.utils";
import { EmbeddedChart } from "../chart/embedded-chart.component";

export function ActivityWorkspace({ chartAvailable, chart, runs, markers, selectedMarker, loading, error, timeZone, onSelectMarker }: {
  chartAvailable: boolean; chart: ActivityChart | null; runs: ActivityRun[]; markers: RunMarker[]; selectedMarker: RunMarker | null;
  loading: boolean; error: string; timeZone: string; onSelectMarker: (marker: RunMarker | null) => void;
}) {
  const selectId = useCallback((id: string) => onSelectMarker(markers.find((marker) => marker.id === id) ?? null), [markers, onSelectMarker]);
  const selectFloor = useCallback((floor: number) => onSelectMarker(markers.find((marker) => marker.floorIndex === floor) ?? null), [markers, onSelectMarker]);
  const selectedRuns = runsForMarker(runs, selectedMarker);
  if (error) return <StatePanel title="Could not load activity" body={error} />;
  if (!chartAvailable) return <StatePanel title="Chart unavailable" body="This session has no stored chart text. Its level remains listed and browsable." />;
  if (!chart) return <StatePanel title={loading ? "Loading session…" : "Chart unavailable"} body={loading ? "Runs and chart pages are loading incrementally." : "No chart data was returned."} />;
  return <section className="flex min-h-0 flex-1 gap-3 p-3">
    <EmbeddedChart chart={chart} markers={markers} selectedMarker={selectedMarker} onMarkerSelect={selectId} onFloorSelect={selectFloor} />
    <aside className="w-80 shrink-0 overflow-y-auto rounded-lg border border-border bg-muted/10 p-3">
      <h2 className="font-heading text-lg font-semibold">Run marker details</h2>
      {!selectedMarker ? <p className="mt-3 text-sm text-muted-foreground">Select a floor marker in the chart to inspect its runs.</p> : <>
        <div className="mt-3 grid grid-cols-2 gap-2 text-sm"><Stat label="Start floor" value={selectedMarker.floorIndex} /><Stat label="Attempts" value={selectedMarker.count} /><Stat label="Clears" value={selectedMarker.clearCount} /><Stat label="Best floor" value={selectedMarker.bestLastFloorIndex} /></div>
        <div className="mt-4 space-y-2">{selectedRuns.map((run) => <div key={run.Id} className="rounded-md border border-border bg-background/60 p-2 text-xs"><div className="flex justify-between font-medium"><span>Run #{run.RunIndex}</span><span>{run.Result}</span></div><div className="mt-1 text-muted-foreground">{run.StartTile} → {run.LastTile ?? "open"} · {formatTime(run.StartedAtUtc, timeZone)}</div><div className="mt-1 text-muted-foreground">{run.InputCount} inputs ({run.InputBytes} B) · {run.HitContextCount} hits ({run.HitContextBytes} B) · {run.LevelPitchPercent ?? "?"}% pitch</div></div>)}</div>
      </>}
    </aside>
  </section>;
}

function StatePanel({ title, body }: { title: string; body: string }) { return <section className="grid min-h-0 flex-1 place-items-center p-8 text-center"><div><h2 className="font-heading text-xl font-semibold">{title}</h2><p className="mt-2 text-sm text-muted-foreground">{body}</p></div></section>; }
function Stat({ label, value }: { label: string; value: string | number }) { return <div className="rounded-md bg-muted/40 p-2"><div className="text-xs text-muted-foreground">{label}</div><div className="font-semibold">{value}</div></div>; }
