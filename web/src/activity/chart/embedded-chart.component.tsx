import { forwardRef, useEffect, useImperativeHandle, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import type { ActivityChart, ActivityRun, RunMarker } from "../activity.model";
import { ChartBridge } from "./chart-bridge";

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
  function EmbeddedChart(
    { chart, markers, selectedMarker, selectedRun, onMarkerSelect, onFloorSelect },
    ref,
  ) {
    const { t } = useTranslation("activity");
    const frameRef = useRef<HTMLIFrameElement>(null);
    const bridgeRef = useRef<ChartBridge | null>(null);
    const callbacksRef = useRef({ onFloorSelect, onMarkerSelect });
    callbacksRef.current = { onFloorSelect, onMarkerSelect };
    const embed = useMemo(resolveEmbedConfig, []);
    const [frameSrc, setFrameSrc] = useState("about:blank");
    const [state, setState] = useState<"loading" | "ready" | "error">(
      embed.errorKey ? "error" : "loading",
    );
    const [error, setError] = useState("");

    useEffect(() => {
      if (!embed.src || !embed.origin) return;
      const frame = frameRef.current;
      if (!frame) return;
      const bridge = new ChartBridge(frame, embed.origin, {
        onReady: () => setState("loading"),
        onLoaded: () => setState("ready"),
        onError: (message) => {
          setError(message);
          setState("error");
        },
        onFloorSelected: (floorIndex) => callbacksRef.current.onFloorSelect(floorIndex),
        onMarkerSelected: (markerId) => callbacksRef.current.onMarkerSelect(markerId),
      });
      bridgeRef.current = bridge;
      const listener = (event: MessageEvent) => bridge.handleMessage(event);
      window.addEventListener("message", listener);
      setFrameSrc(embed.src);
      return () => {
        window.removeEventListener("message", listener);
        if (bridgeRef.current === bridge) bridgeRef.current = null;
      };
    }, [embed.origin, embed.src]);

    const chartLevelSessionId = chart?.LevelSessionId;
    const chartLevelText = chart?.LevelText;
    const chartFloorCount = chart?.FloorCount;

    useEffect(() => {
      if (
        chartLevelSessionId === undefined ||
        chartLevelText === undefined ||
        chartFloorCount === undefined
      )
        return;
      setState("loading");
      bridgeRef.current?.load({
        LevelSessionId: chartLevelSessionId,
        LevelText: chartLevelText,
        FloorCount: chartFloorCount,
      });
    }, [chartFloorCount, chartLevelSessionId, chartLevelText]);

    useEffect(() => {
      bridgeRef.current?.setMarkers(markers);
    }, [markers]);

    const selectedMarkerId = selectedMarker?.id ?? null;
    const selectedMarkerFloor = selectedMarker?.floorIndex ?? null;
    const selectedRunId = selectedRun?.Id ?? null;
    const selectedRunStart = selectedRun?.StartTile ?? null;
    const selectedRunEnd = selectedRun
      ? Math.max(selectedRun.StartTile, selectedRun.LastTile ?? selectedRun.StartTile)
      : null;

    useEffect(() => {
      if (state !== "ready" || selectedMarkerId === null || selectedMarkerFloor === null) return;
      bridgeRef.current?.focus(selectedMarkerFloor, selectedMarkerId);
    }, [selectedMarkerFloor, selectedMarkerId, state]);

    useEffect(() => {
      if (state !== "ready") return;
      if (selectedRunId === null || selectedRunStart === null || selectedRunEnd === null) {
        bridgeRef.current?.clearRunFocus();
        return;
      }
      bridgeRef.current?.focusRun(selectedRunStart, selectedRunEnd);
    }, [selectedRunEnd, selectedRunId, selectedRunStart, state]);

    useImperativeHandle(
      ref,
      () => ({
        refocusSelection() {
          if (state !== "ready") return;
          if (selectedRunStart !== null && selectedRunEnd !== null) {
            bridgeRef.current?.focusRun(selectedRunStart, selectedRunEnd);
            return;
          }
          if (selectedMarkerFloor === null || selectedMarkerId === null) return;
          bridgeRef.current?.focus(selectedMarkerFloor, selectedMarkerId);
        },
      }),
      [selectedMarkerFloor, selectedMarkerId, selectedRunEnd, selectedRunStart, state],
    );

    return (
      <div className="relative min-h-0 flex-1 overflow-hidden bg-black/30">
        {embed.src ? (
          <iframe
            ref={frameRef}
            src={frameSrc}
            title={t("chart.viewerTitle")}
            className="absolute inset-0 size-full border-0"
          />
        ) : null}
        {state === "loading" ? <ChartOverlay>{t("chart.loadingViewer")}</ChartOverlay> : null}
        {state === "error" ? (
          <ChartOverlay>
            {error || (embed.errorKey ? t(embed.errorKey) : t("chart.viewerLoadFailed"))}
          </ChartOverlay>
        ) : null}
      </div>
    );
  },
);

function resolveEmbedConfig(): {
  src: string;
  origin: string;
  errorKey: "chart.missingEmbedUrl" | "chart.invalidEmbedUrl" | null;
} {
  const configuredUrl = import.meta.env.VITE_WEB_ADOFAI_EMBED_URL?.trim();
  if (!configuredUrl)
    return {
      src: "",
      origin: "",
      errorKey: "chart.missingEmbedUrl",
    };
  try {
    const url = new URL(configuredUrl);
    url.searchParams.set("parentOrigin", window.location.origin);
    return { src: url.toString(), origin: url.origin, errorKey: null };
  } catch {
    return {
      src: "",
      origin: "",
      errorKey: "chart.invalidEmbedUrl",
    };
  }
}

function ChartOverlay({ children }: { children: React.ReactNode }) {
  return (
    <div className="pointer-events-none absolute inset-0 grid place-items-center bg-background/80 text-sm text-muted-foreground backdrop-blur-sm">
      {children}
    </div>
  );
}
