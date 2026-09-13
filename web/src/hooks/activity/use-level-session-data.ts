import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";
import { useTranslation } from "react-i18next";

import { useApiPromise } from "@/api/app-api-provider";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import { activityQueryKeys, removeRunById } from "@/state/activity/activity-queries";

export function useLevelSessionData(
  id: string | null,
  appSessionIds: string[],
  chartAvailable: boolean,
  revision: number,
) {
  const { t } = useTranslation("activity");
  const apiPromise = useApiPromise();
  const queryClient = useQueryClient();
  const levelKey = id ? activityQueryKeys.logicalLevel(id) : ["activity", "logical-level", null];
  const runsKey = id
    ? activityQueryKeys.logicalLevelRuns(id, appSessionIds)
    : ["activity", "logical-level", null, "runs"];
  const chartKey = id
    ? activityQueryKeys.logicalLevelChart(id)
    : ["activity", "logical-level", null, "chart"];

  const overview = useQuery({
    queryKey: [...levelKey, revision],
    queryFn: async () => (await apiPromise).activity.getLogicalLevel(id as string),
    enabled: Boolean(id),
  });
  const runs = useQuery({
    queryKey: [...runsKey, revision],
    queryFn: async () =>
      (await apiPromise).activity.listLogicalLevelRuns(id as string, appSessionIds, (items) => {
        queryClient.setQueryData([...runsKey, revision], items);
      }),
    enabled: Boolean(id),
  });
  const chart = useQuery({
    queryKey: chartKey,
    queryFn: async () => (await apiPromise).activity.getLogicalLevelChart(id as string),
    enabled: Boolean(id && chartAvailable),
  });

  const updateRun = useCallback(
    (runId: string, update: Partial<ActivityRun>) => {
      queryClient.setQueryData<ActivityRun[]>([...runsKey, revision], (current = []) =>
        current.map((run) => (run.id === runId ? { ...run, ...update } : run)),
      );
    },
    [queryClient, revision, runsKey],
  );
  const removeRun = useCallback(
    (runId: string) => {
      queryClient.setQueryData<ActivityRun[]>([...runsKey, revision], (current = []) =>
        removeRunById(current, runId),
      );
    },
    [queryClient, revision, runsKey],
  );

  const error = overview.error
    ? localizedErrorMessage(overview.error, t("errors.loadLevel"))
    : runs.error
      ? localizedErrorMessage(runs.error, t("errors.loadRuns"))
      : "";
  const chartError = chart.error ? localizedErrorMessage(chart.error, t("errors.loadChart")) : "";
  return {
    overview: overview.data ?? null,
    runs: runs.data ?? [],
    chart: chartAvailable ? (chart.data ?? null) : null,
    loading: overview.isPending || runs.isPending || (chartAvailable && chart.isPending),
    error,
    chartError,
    updateRun,
    removeRun,
  };
}
