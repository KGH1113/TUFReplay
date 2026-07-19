import { type RefObject, useEffect, useRef, useState } from "react";

import type { ActivityChart, ActivityLevelSessionOverview, ActivityRun } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";

export function useLevelSessionData(
  id: string | null,
  chartAvailable: boolean,
  revision: number,
  gatewayRef: RefObject<ActivityGateway | null>,
) {
  const [overview, setOverview] = useState<ActivityLevelSessionOverview | null>(null);
  const [runs, setRuns] = useState<ActivityRun[]>([]);
  const [chart, setChart] = useState<ActivityChart | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const selectedLevelIdRef = useRef<string | null>(null);
  const loadedChartLevelIdRef = useRef<string | null>(null);

  useEffect(() => {
    void revision;
    const gateway = gatewayRef.current;
    if (!id || !gateway) {
      selectedLevelIdRef.current = null;
      loadedChartLevelIdRef.current = null;
      setOverview(null);
      setRuns([]);
      setChart(null);
      return;
    }
    const { levelChanged, shouldLoadChart } = planLevelSessionRefresh(
      id,
      chartAvailable,
      selectedLevelIdRef.current,
      loadedChartLevelIdRef.current,
    );
    selectedLevelIdRef.current = id;
    let active = true;
    setLoading(true);
    setError("");
    if (levelChanged) {
      setOverview(null);
      setRuns([]);
    }
    if (!chartAvailable) {
      loadedChartLevelIdRef.current = null;
      setChart(null);
    } else if (shouldLoadChart) {
      setChart(null);
    }
    const tasks: Promise<unknown>[] = [
      gateway.getLevelSession(id).then((value) => {
        if (active) setOverview(value);
      }),
      gateway
        .listAllRuns(
          id,
          levelChanged
            ? (value) => {
                if (active) setRuns(value);
              }
            : undefined,
        )
        .then((value) => {
          if (active) setRuns(value);
        }),
    ];
    if (shouldLoadChart)
      tasks.push(
        gateway.getChart(id).then((value) => {
          if (active) {
            loadedChartLevelIdRef.current = id;
            setChart(value);
          }
        }),
      );
    Promise.all(tasks)
      .catch((cause) => {
        if (active)
          setError(cause instanceof Error ? cause.message : "Could not load level session");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [chartAvailable, gatewayRef, id, revision]);

  return { overview, runs, chart, loading, error };
}

export function planLevelSessionRefresh(
  id: string,
  chartAvailable: boolean,
  selectedLevelId: string | null,
  loadedChartLevelId: string | null,
) {
  return {
    levelChanged: selectedLevelId !== id,
    shouldLoadChart: chartAvailable && loadedChartLevelId !== id,
  };
}
