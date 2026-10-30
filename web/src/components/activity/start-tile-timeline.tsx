import { useTranslation } from "react-i18next";
import { useElementWidth } from "@/hooks/shared/use-element-width";
import type { RunMarker } from "@/models/activity/activity-model";
import { tileProgressPercent } from "@/models/activity/run-progress";
import { layoutTimelineMarkers } from "@/models/activity/start-tile-timeline";
import { cn } from "@/shared/lib/cn";
import { ToggleGroup, ToggleGroupItem } from "@/shared/ui/toggle-group";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

const MARKER_SIZE = 24;
const MARKER_DISTANCE = 28;

/**
 * The level as a line from start to end, with a marker wherever attempts began.
 * Pick a marker to show only those attempts; pick it again to show every attempt.
 */
export function StartTileTimeline({
  markers,
  floorCount,
  selectedMarker,
  onSelect,
  className,
}: {
  markers: RunMarker[];
  floorCount: number;
  selectedMarker: RunMarker | null;
  onSelect: (marker: RunMarker | null) => void;
  className?: string;
}) {
  const { t } = useTranslation("activity");
  const track = useElementWidth<HTMLDivElement>();
  const floors = Math.max(1, floorCount);
  const centers = layoutTimelineMarkers(
    markers.map((marker) => marker.floorIndex / floors),
    track.width,
    MARKER_SIZE,
    MARKER_DISTANCE,
  );
  const selectedIndex = markers.findIndex((marker) => marker.id === selectedMarker?.id);
  const reach =
    selectedIndex >= 0
      ? Math.min(1, markers[selectedIndex].bestLastFloorIndex / floors) * track.width
      : null;

  return (
    <div className={cn("flex min-w-0 items-center gap-2.5", className)}>
      <span className="shrink-0 text-[11px] font-medium text-subtle-foreground">
        {t("attempts.timelineStart")}
      </span>
      <div ref={track.ref} className="relative h-8 min-w-0 flex-1">
        <span
          aria-hidden="true"
          className="absolute inset-x-0 top-1/2 h-0.5 -translate-y-1/2 rounded-full bg-foreground/12"
        />
        {selectedIndex >= 0 && reach !== null ? (
          <span
            aria-hidden="true"
            className="absolute top-1/2 h-0.5 -translate-y-1/2 rounded-full bg-primary/70"
            style={{
              left: centers[selectedIndex],
              width: Math.max(0, reach - centers[selectedIndex]),
            }}
          />
        ) : null}
        {track.width > 0 ? (
          <ToggleGroup
            type="single"
            spacing={0}
            value={selectedMarker?.id ?? ""}
            onValueChange={(value) =>
              onSelect(markers.find((marker) => marker.id === value) ?? null)
            }
            aria-label={t("attempts.filterLabel")}
            className="absolute inset-0 block w-auto"
          >
            {markers.map((marker, index) => {
              const percent = tileProgressPercent(marker.floorIndex, floors);
              const best = tileProgressPercent(marker.bestLastFloorIndex, floors);
              const selected = index === selectedIndex;
              return (
                <Tooltip key={marker.id}>
                  <TooltipTrigger asChild>
                    <ToggleGroupItem
                      value={marker.id}
                      aria-label={t("attempts.markerLabel", {
                        tile: marker.floorIndex,
                        percent,
                        count: marker.count,
                      })}
                      className={cn(
                        "absolute top-1/2 h-6! min-w-6! -translate-x-1/2 -translate-y-1/2 rounded-full! px-1.5! text-[11px] font-semibold tabular-nums shadow-sm ring-1 transition-[background-color,color,box-shadow,transform] hover:z-10 active:scale-90 motion-reduce:active:scale-100",
                        selected
                          ? "z-10 bg-foreground! text-background! ring-foreground"
                          : cn(
                              "bg-secondary text-foreground hover:bg-accent",
                              marker.clearCount > 0 ? "ring-primary/80" : "ring-border",
                              selectedIndex >= 0 && "opacity-60 hover:opacity-100",
                            ),
                      )}
                      style={{ left: centers[index] }}
                    >
                      {marker.count}
                    </ToggleGroupItem>
                  </TooltipTrigger>
                  <TooltipContent side="top">
                    {t("attempts.markerTooltip", {
                      tile: marker.floorIndex,
                      percent,
                      best,
                      count: marker.count,
                    })}
                    {marker.clearCount > 0
                      ? ` · ${t("attempts.markerClears", { count: marker.clearCount })}`
                      : ""}
                  </TooltipContent>
                </Tooltip>
              );
            })}
          </ToggleGroup>
        ) : null}
      </div>
      <span className="shrink-0 text-[11px] font-medium text-subtle-foreground">
        {t("attempts.timelineEnd")}
      </span>
    </div>
  );
}
