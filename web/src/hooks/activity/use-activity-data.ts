import { IpcVersionMismatchError } from "@adofai-ipc/client";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useApiPromise, useMockEnabled } from "@/api/app-api-provider";
import type { AppSession, ConnectionStatus } from "@/models/activity/activity-model";
import { diagnosticErrorMessage } from "@/models/activity/localized-error";
import type { Health } from "@/models/health/health-model";
import { ApiError } from "@/shared/errors/api-error";
import {
  activityQueryKeys,
  mergeRecentAppSessions,
  RECENT_ACTIVITY_SESSION_LIMIT,
} from "@/state/activity/activity-queries";

const POLL_INTERVAL_MS = 3000;

interface ActivityDataSnapshot {
  sessions: AppSession[];
  health: Health;
}

export function useActivityData() {
  const apiPromise = useApiPromise();
  const mockEnabled = useMockEnabled();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: activityQueryKeys.sessions,
    queryFn: async () => {
      const api = await apiPromise;
      const health = await api.health.get();
      const current = queryClient.getQueryData<ActivityDataSnapshot>(
        activityQueryKeys.sessions,
      )?.sessions;
      if (current) {
        const recent = await api.activity.listAppSessions(0, RECENT_ACTIVITY_SESSION_LIMIT);
        return { sessions: mergeRecentAppSessions(current, recent), health };
      }
      const sessions = await api.activity.listAllAppSessions((items) => {
        queryClient.setQueryData<ActivityDataSnapshot>(activityQueryKeys.sessions, {
          sessions: items,
          health,
        });
      });
      return { sessions, health };
    },
    refetchInterval: POLL_INTERVAL_MS,
    refetchIntervalInBackground: false,
  });

  return {
    sessions: query.data?.sessions ?? [],
    health: query.status === "success" ? query.data.health : null,
    status: statusForQuery(query.status, query.error),
    error: diagnosticErrorMessage(query.error),
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
