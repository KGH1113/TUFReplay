import { type QueryClient, queryOptions } from "@tanstack/react-query";
import type { AppApi } from "@/api/app-api";
import type { ActivityRun } from "@/models/activity/activity-model";
import { activityQueryKeys } from "@/state/activity/activity-queries";

export interface LogicalLevelRunsSnapshot {
  items: ActivityRun[];
  complete: boolean;
}

export function logicalLevelRunsQuery(
  queryClient: QueryClient,
  apiPromise: Promise<AppApi>,
  id: string | null,
  date: string | null,
  appSessionIds: string[],
) {
  const queryKey = activityQueryKeys.logicalLevelRuns(id ?? "", date);
  return queryOptions({
    queryKey,
    enabled: Boolean(id),
    queryFn: async ({ signal }): Promise<LogicalLevelRunsSnapshot> => {
      const api = await apiPromise;
      signal.throwIfAborted();
      const hasPreviousSnapshot =
        queryClient.getQueryData<LogicalLevelRunsSnapshot>(queryKey) !== undefined;
      const items = await api.activity.listLogicalLevelRuns(id as string, appSessionIds, (page) => {
        // Stop the API's pagination loop once an obsolete in-flight page returns.
        signal.throwIfAborted();
        // A refresh must not replace the existing list with a partial first page.
        if (!hasPreviousSnapshot) {
          queryClient.setQueryData<LogicalLevelRunsSnapshot>(queryKey, {
            items: page,
            complete: false,
          });
        }
      });
      signal.throwIfAborted();
      return { items, complete: true };
    },
  });
}
