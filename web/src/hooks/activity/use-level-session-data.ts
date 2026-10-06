import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";

import { useApiPromise } from "@/api/app-api-provider";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import { activityQueryKeys, removeRunById } from "@/state/activity/activity-queries";
import {
  type LogicalLevelRunsSnapshot,
  logicalLevelRunsQuery,
} from "@/state/activity/logical-level-runs-query";

export function useLevelSessionData(
  id: string | null,
  appSessionIds: string[],
  chartAvailable: boolean,
  revision: number,
  date: string | null,
) {
  const { t } = useTranslation("activity");
  const apiPromise = useApiPromise();
  const queryClient = useQueryClient();
  const levelKey = id ? activityQueryKeys.logicalLevel(id) : ["activity", "logical-level", null];
  const runsOptions = logicalLevelRunsQuery(queryClient, apiPromise, id, date, appSessionIds);
  const runsKey = runsOptions.queryKey;
  const chartKey = id
    ? activityQueryKeys.logicalLevelChart(id)
    : ["activity", "logical-level", null, "chart"];

  const overview = useQuery({
    queryKey: levelKey,
    queryFn: async () => (await apiPromise).activity.getLogicalLevel(id as string),
    enabled: Boolean(id),
  });
  const runs = useQuery(runsOptions);
  const chart = useQuery({
    queryKey: chartKey,
    queryFn: async ({ signal }) =>
      (await apiPromise).activity.getLogicalLevelChart(id as string, { signal }),
    enabled: Boolean(id && chartAvailable),
  });

  const appSessionScope = JSON.stringify([...appSessionIds].sort());
  const previousRequest = useRef({ id, date, revision, appSessionScope });
  useEffect(() => {
    const previous = previousRequest.current;
    previousRequest.current = { id, date, revision, appSessionScope };
    if (
      !id ||
      previous.id !== id ||
      (previous.date === date &&
        previous.revision === revision &&
        previous.appSessionScope === appSessionScope)
    )
      return;
    void queryClient.invalidateQueries({ queryKey: levelKey, exact: true });
    if (previous.date === date) {
      // Cancel obsolete pagination before fetching the new activity snapshot.
      void queryClient
        .cancelQueries({ queryKey: runsKey, exact: true }, { revert: false })
        .then(() => queryClient.invalidateQueries({ queryKey: runsKey, exact: true }));
    }
  }, [appSessionScope, date, id, levelKey, queryClient, revision, runsKey]);

  const updateRun = useCallback(
    async (runId: string, update: Partial<ActivityRun>) => {
      await queryClient.cancelQueries({ queryKey: runsKey, exact: true }, { revert: false });
      queryClient.setQueryData<LogicalLevelRunsSnapshot>(runsKey, (current) =>
        current
          ? {
              ...current,
              items: current.items.map((run) => (run.id === runId ? { ...run, ...update } : run)),
            }
          : current,
      );
      void queryClient.invalidateQueries({ queryKey: runsKey, exact: true });
    },
    [queryClient, runsKey],
  );
  const removeRun = useCallback(
    async (runId: string) => {
      await queryClient.cancelQueries({ queryKey: runsKey, exact: true }, { revert: false });
      queryClient.setQueryData<LogicalLevelRunsSnapshot>(runsKey, (current) =>
        current ? { ...current, items: removeRunById(current.items, runId) } : current,
      );
      void queryClient.invalidateQueries({ queryKey: runsKey, exact: true });
    },
    [queryClient, runsKey],
  );
  const retry = useCallback(async () => {
    await queryClient.cancelQueries({ queryKey: runsKey, exact: true }, { revert: false });
    await Promise.all([overview.refetch(), runs.refetch()]);
  }, [overview.refetch, queryClient, runs.refetch, runsKey]);

  const error = overview.error
    ? localizedErrorMessage(overview.error, t("errors.loadLevel"))
    : runs.error
      ? localizedErrorMessage(runs.error, t("errors.loadRuns"))
      : "";
  const chartError = chart.error ? localizedErrorMessage(chart.error, t("errors.loadChart")) : "";
  return {
    overview: overview.data ?? null,
    runs: runs.data?.items ?? [],
    runsSettled: Boolean(runs.data?.complete && !runs.isFetching && !runs.isError),
    chart: chartAvailable ? (chart.data ?? null) : null,
    loading: overview.isPending || runs.isPending || (chartAvailable && chart.isPending),
    error,
    chartError,
    updateRun,
    removeRun,
    retry,
  };
}
