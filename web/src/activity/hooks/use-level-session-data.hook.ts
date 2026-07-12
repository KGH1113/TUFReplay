import { useEffect, useState, type RefObject } from "react";

import type { ActivityChart, ActivityLevelSessionOverview, ActivityRun } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";

export function useLevelSessionData(id: string | null, chartAvailable: boolean, revision: number, gatewayRef: RefObject<ActivityGateway | null>) {
  const [overview, setOverview] = useState<ActivityLevelSessionOverview | null>(null);
  const [runs, setRuns] = useState<ActivityRun[]>([]);
  const [chart, setChart] = useState<ActivityChart | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const gateway = gatewayRef.current;
    if (!id || !gateway) { setOverview(null); setRuns([]); setChart(null); return; }
    let active = true;
    setLoading(true); setError(""); setRuns([]); setChart(null);
    const tasks: Promise<unknown>[] = [
      gateway.getLevelSession(id).then((value) => { if (active) setOverview(value); }),
      gateway.listAllRuns(id, (value) => { if (active) setRuns(value); }),
    ];
    if (chartAvailable) tasks.push(gateway.getChart(id).then((value) => { if (active) setChart(value); }));
    Promise.all(tasks).catch((cause) => { if (active) setError(cause instanceof Error ? cause.message : "Could not load level session"); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [chartAvailable, gatewayRef, id, revision]);

  return { overview, runs, chart, loading, error };
}
