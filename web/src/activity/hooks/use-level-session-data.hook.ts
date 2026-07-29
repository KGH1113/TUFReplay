import { type RefObject, useCallback, useEffect, useRef, useState } from "react";

import i18n from "../../i18n/i18n";
import type { ActivityChart, ActivityLogicalLevelOverview, ActivityRun } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { localizedErrorMessage } from "../lib/localized-error";

export function useLevelSessionData(
  id: string | null,
  appSessionIds: string[],
  chartAvailable: boolean,
  revision: number,
  gatewayRef: RefObject<ActivityGateway | null>,
) {
  const appSessionKey = appSessionIds.join("\0");
  const [overview, setOverview] = useState<ActivityLogicalLevelOverview | null>(null);
  const [runs, setRuns] = useState<ActivityRun[]>([]);
  const [chart, setChart] = useState<ActivityChart | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const selectedLevelIdRef = useRef<string | null>(null);
  const loadedChartLevelIdRef = useRef<string | null>(null);

  useEffect(() => {
    void revision;
    const requestedAppSessionIds = appSessionKey ? appSessionKey.split("\0") : [];
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
      gateway.getLogicalLevel(id).then((value) => {
        if (active) setOverview(value);
      }),
      gateway
        .listAllLogicalLevelRuns(
          id,
          requestedAppSessionIds,
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
        gateway.getLogicalLevelChart(id).then((value) => {
          if (active) {
            loadedChartLevelIdRef.current = id;
            setChart(value);
          }
        }),
      );
    Promise.all(tasks)
      .catch((cause) => {
        if (active)
          setError(localizedErrorMessage(cause, i18n.t("errors.loadSession", { ns: "activity" })));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [appSessionKey, chartAvailable, gatewayRef, id, revision]);

  const updateRun = useCallback((runId: string, update: Partial<ActivityRun>) => {
    setRuns((current) => current.map((run) => (run.Id === runId ? { ...run, ...update } : run)));
  }, []);

  const removeRun = useCallback((runId: string) => {
    setRuns((current) => removeRunById(current, runId));
  }, []);

  return { overview, runs, chart, loading, error, updateRun, removeRun };
}

export function removeRunById(runs: ActivityRun[], runId: string) {
  return runs.filter((run) => run.Id !== runId);
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
