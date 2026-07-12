import { useEffect, useMemo, useRef, useState } from "react";

import type { ActivityChart, RunMarker } from "../activity.model";
import { ChartBridge } from "./chart-bridge";

const DEFAULT_EMBED_URL = "https://web-adofai.impl1113.dev/embed/chart";

export function EmbeddedChart({
  chart,
  markers,
  selectedMarker,
  onMarkerSelect,
  onFloorSelect,
}: {
  chart: ActivityChart | null;
  markers: RunMarker[];
  selectedMarker: RunMarker | null;
  onMarkerSelect: (id: string) => void;
  onFloorSelect: (floorIndex: number) => void;
}) {
  const frameRef = useRef<HTMLIFrameElement>(null);
  const bridgeRef = useRef<ChartBridge | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");
  const [error, setError] = useState("");
  const embed = useMemo(() => {
    const url = new URL(import.meta.env.VITE_WEB_ADOFAI_EMBED_URL || DEFAULT_EMBED_URL);
    url.searchParams.set("parentOrigin", window.location.origin);
    return { src: url.toString(), origin: url.origin };
  }, []);

  useEffect(() => {
    const frame = frameRef.current;
    if (!frame) return;
    const bridge = new ChartBridge(frame, embed.origin, {
      onReady: () => setState("loading"),
      onLoaded: () => setState("ready"),
      onError: (message) => { setError(message); setState("error"); },
      onFloorSelected: onFloorSelect,
      onMarkerSelected: onMarkerSelect,
    });
    bridgeRef.current = bridge;
    const listener = (event: MessageEvent) => bridge.handleMessage(event);
    window.addEventListener("message", listener);
    return () => {
      window.removeEventListener("message", listener);
      if (bridgeRef.current === bridge) bridgeRef.current = null;
    };
  }, [embed.origin, onFloorSelect, onMarkerSelect]);

  useEffect(() => {
    if (!chart) return;
    setState("loading");
    bridgeRef.current?.load(chart, markers);
  }, [chart, markers]);

  useEffect(() => {
    if (selectedMarker) bridgeRef.current?.focus(selectedMarker.floorIndex, selectedMarker.id);
  }, [selectedMarker]);

  return (
    <div className="relative min-h-[24rem] flex-1 overflow-hidden rounded-lg border border-border bg-black/30">
      <iframe ref={frameRef} src={embed.src} title="ADOFAI level chart" className="absolute inset-0 size-full border-0" />
      {state === "loading" ? <ChartOverlay>Loading chart…</ChartOverlay> : null}
      {state === "error" ? <ChartOverlay>{error || "The embedded chart could not be loaded."}</ChartOverlay> : null}
    </div>
  );
}

function ChartOverlay({ children }: { children: React.ReactNode }) {
  return <div className="pointer-events-none absolute inset-0 grid place-items-center bg-background/80 text-sm text-muted-foreground backdrop-blur-sm">{children}</div>;
}
