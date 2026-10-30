import { useQuery } from "@tanstack/react-query";
import { useApiPromise } from "@/api/app-api-provider";
import type { RunJudgmentTimeline } from "@/models/activity/judgment-timeline";
import { ApiError } from "@/shared/errors/api-error";
import { activityQueryKeys } from "@/state/activity/activity-queries";

/** A saved run never changes its judgments, so a loaded timeline is kept for the whole visit. */
export function useRunJudgmentTimeline(runId: string, enabled: boolean) {
  const apiPromise = useApiPromise();
  const query = useQuery<RunJudgmentTimeline | null>({
    queryKey: activityQueryKeys.judgmentTimeline(runId),
    queryFn: async () => (await apiPromise).run.loadJudgmentTimeline(runId),
    enabled,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: 30 * 60_000,
    // The mod holds the request while an attempt is judged, so a long pause can time it out;
    // keep asking until the attempt ends. Any other failure is retried once.
    retry: (failureCount, error) =>
      (error instanceof ApiError && error.code === "ipc_timeout") || failureCount < 1,
  });
  return { timeline: query.data ?? null, loading: query.isPending && enabled };
}
