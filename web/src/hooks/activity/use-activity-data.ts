import { IpcVersionMismatchError } from "@adofai-ipc/client";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useApiPromise, useMockEnabled } from "@/api/app-api-provider";
import type { AppSession, ConnectionStatus } from "@/models/activity/activity-model";
import { ApiError } from "@/shared/errors/api-error";
import {
  activityQueryKeys,
  mergeRecentAppSessions,
  RECENT_ACTIVITY_SESSION_LIMIT,
} from "@/state/activity/activity-queries";

const POLL_INTERVAL_MS = 3000;

export function useActivityData() {
  const apiPromise = useApiPromise();
  const mockEnabled = useMockEnabled();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: activityQueryKeys.sessions,
    queryFn: async () => {
      const api = await apiPromise;
      await api.health.get();
      const current = queryClient.getQueryData<AppSession[]>(activityQueryKeys.sessions);
      if (current) {
        const recent = await api.activity.listAppSessions(0, RECENT_ACTIVITY_SESSION_LIMIT);
        return mergeRecentAppSessions(current, recent);
      }
      return api.activity.listAllAppSessions((items) => {
        queryClient.setQueryData(activityQueryKeys.sessions, items);
      });
    },
    refetchInterval: POLL_INTERVAL_MS,
    refetchIntervalInBackground: false,
  });

  return {
    sessions: query.data ?? [],
    status: statusForQuery(query.status, query.error),
    error: query.error instanceof Error ? query.error.message : "",
    versionMismatch: query.error instanceof IpcVersionMismatchError ? query.error.direction : null,
    retry: query.refetch,
    mockEnabled,
  };
}

function statusForQuery(status: "pending" | "error" | "success", cause: unknown): ConnectionStatus {
  if (status === "pending") return "connecting";
  if (status === "success") return "online";
  return connectionStatusForError(cause);
}

export function connectionStatusForError(cause: unknown): ConnectionStatus {
  return cause instanceof IpcVersionMismatchError ||
    (cause instanceof ApiError && cause.kind === "protocol")
    ? "incompatible"
    : "error";
}
