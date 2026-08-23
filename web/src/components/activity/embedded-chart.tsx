import { forwardRef, useImperativeHandle } from "react";
import { useEmbeddedChartViewModel } from "@/hooks/activity/use-embedded-chart";
import type { ActivityChart, ActivityRun, RunMarker } from "@/models/activity/activity-model";

export interface EmbeddedChartHandle {
  refocusSelection(): void;
}

interface EmbeddedChartProps {
  chart: ActivityChart | null;
  markers: RunMarker[];
  selectedMarker: RunMarker | null;
  selectedRun: ActivityRun | null;
  onMarkerSelect: (id: string) => void;
  onFloorSelect: (floorIndex: number) => void;
}

export const EmbeddedChart = forwardRef<EmbeddedChartHandle, EmbeddedChartProps>(
  function EmbeddedChart(props, ref) {
    const viewModel = useEmbeddedChartViewModel(props);

    useImperativeHandle(ref, () => ({ refocusSelection: viewModel.refocusSelection }), [
      viewModel.refocusSelection,
    ]);

    return (
      <div className="relative min-h-0 flex-1 overflow-hidden bg-black/30">
        {viewModel.showFrame ? (
          <iframe
            ref={viewModel.frameRef}
            src={viewModel.frameSrc}
            title={viewModel.frameTitle}
            className="absolute inset-0 size-full border-0"
          />
        ) : null}
        {viewModel.overlayText ? <ChartOverlay>{viewModel.overlayText}</ChartOverlay> : null}
      </div>
    );
  },
);

function ChartOverlay({ children }: { children: React.ReactNode }) {
  return (
    <div className="pointer-events-none absolute inset-0 grid place-items-center bg-background/80 text-sm text-muted-foreground backdrop-blur-sm">
      {children}
    </div>
  );
}
