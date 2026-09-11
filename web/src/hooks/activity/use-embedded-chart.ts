import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ActivityChart, ActivityRun, RunMarker } from "@/models/activity/activity-model";
import { ChartEmbedClient } from "@/shared/clients/chart-embed-client";

interface EmbeddedChartViewModelOptions {
  chart: ActivityChart | null;
  markers: RunMarker[];
  selectedMarker: RunMarker | null;
  selectedRun: ActivityRun | null;
  onMarkerSelect: (id: string) => void;
  onFloorSelect: (floorIndex: number) => void;
}

export function useEmbeddedChartViewModel({
  chart,
  markers,
  selectedMarker,
  selectedRun,
  onMarkerSelect,
  onFloorSelect,
}: EmbeddedChartViewModelOptions) {
  const { t } = useTranslation("activity");
  const frameRef = useRef<HTMLIFrameElement>(null);
  const clientRef = useRef<ChartEmbedClient | null>(null);
  const callbacksRef = useRef({ onFloorSelect, onMarkerSelect });
  callbacksRef.current = { onFloorSelect, onMarkerSelect };

  const embed = useMemo(resolveEmbedConfig, []);
  const [frameSrc, setFrameSrc] = useState("about:blank");
  const [status, setStatus] = useState<"loading" | "ready" | "error">(
    embed.errorKey ? "error" : "loading",
  );
  const [error, setError] = useState("");

  useEffect(() => {
    if (!embed.src || !embed.origin) return;
    const frame = frameRef.current;
    if (!frame) return;

    const client = new ChartEmbedClient(frame, embed.origin, {
      onReady: () => setStatus("loading"),
      onLoaded: () => setStatus("ready"),
      onError: (message) => {
        setError(message);
        setStatus("error");
      },
      onFloorSelected: (floorIndex) => callbacksRef.current.onFloorSelect(floorIndex),
      onMarkerSelected: (markerId) => callbacksRef.current.onMarkerSelect(markerId),
    });
    clientRef.current = client;
    const listener = (event: MessageEvent) => client.handleMessage(event);
    window.addEventListener("message", listener);
    setFrameSrc(embed.src);
    return () => {
      window.removeEventListener("message", listener);
      if (clientRef.current === client) clientRef.current = null;
    };
  }, [embed.origin, embed.src]);

  const chartLevelSessionId = chart?.levelSessionId;
  const chartLevelText = chart?.levelText;
  const chartFloorCount = chart?.floorCount;

  useEffect(() => {
    if (
      chartLevelSessionId === undefined ||
      chartLevelText === undefined ||
      chartFloorCount === undefined
    )
      return;
    setStatus("loading");
    clientRef.current?.load({
      levelText: chartLevelText,
    });
  }, [chartFloorCount, chartLevelSessionId, chartLevelText]);

  useEffect(() => {
    clientRef.current?.setMarkers(markers);
  }, [markers]);

  const selectedMarkerId = selectedMarker?.id ?? null;
  const selectedMarkerFloor = selectedMarker?.floorIndex ?? null;
  const selectedRunId = selectedRun?.id ?? null;
  const selectedRunStart = selectedRun?.startTile ?? null;
  const selectedRunEnd = selectedRun
    ? Math.max(selectedRun.startTile, selectedRun.lastTile ?? selectedRun.startTile)
    : null;

  useEffect(() => {
    if (status !== "ready" || selectedMarkerId === null || selectedMarkerFloor === null) return;
    clientRef.current?.focus(selectedMarkerFloor, selectedMarkerId);
  }, [selectedMarkerFloor, selectedMarkerId, status]);

  useEffect(() => {
    if (status !== "ready") return;
    if (selectedRunId === null || selectedRunStart === null || selectedRunEnd === null) {
      clientRef.current?.clearRunFocus();
      return;
    }
    clientRef.current?.focusRun(selectedRunStart, selectedRunEnd);
  }, [selectedRunEnd, selectedRunId, selectedRunStart, status]);

  const refocusSelection = useCallback(() => {
    if (status !== "ready") return;
    if (selectedRunStart !== null && selectedRunEnd !== null) {
      clientRef.current?.focusRun(selectedRunStart, selectedRunEnd);
      return;
    }
    if (selectedMarkerFloor === null || selectedMarkerId === null) return;
    clientRef.current?.focus(selectedMarkerFloor, selectedMarkerId);
  }, [selectedMarkerFloor, selectedMarkerId, selectedRunEnd, selectedRunStart, status]);

  const overlayText =
    status === "loading"
      ? t("chart.loadingViewer")
      : status === "error"
        ? error || (embed.errorKey ? t(embed.errorKey) : t("chart.viewerLoadFailed"))
        : null;

  return {
    frameRef,
    frameSrc,
    frameTitle: t("chart.viewerTitle"),
    showFrame: Boolean(embed.src),
    overlayText,
    refocusSelection,
  };
}

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
