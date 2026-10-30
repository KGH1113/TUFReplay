import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useState } from "react";
import { useApiPromise, useMockEnabled } from "@/api/app-api-provider";
import type { AppSession, ConnectionStatus } from "@/models/activity/activity-model";
import { diagnosticErrorMessage } from "@/models/activity/localized-error";
import type { Health } from "@/models/health/health-model";
import { ApiError } from "@/shared/errors/api-error";
import { IpcProtocolMismatchError } from "@/shared/errors/ipc-protocol-mismatch-error";
import {
  activityQueryKeys,
  adjustLockedRunCounts,
  mergeRecentAppSessions,
  RECENT_ACTIVITY_SESSION_LIMIT,
  refreshesOnActivityChange,
} from "@/state/activity/activity-queries";
import {
  type ActivityConnectionSnapshot,
  observeActivityConnection,
} from "@/state/activity/connection-lifecycle";

interface ActivityDataSnapshot {
  sessions: AppSession[];
  health: Health;
}

export function useActivityData() {
  const apiPromise = useApiPromise();
  const mockEnabled = useMockEnabled();
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<ActivityConnectionSnapshot>({
    session: null,
    recorder: null,
  });
  useEffect(() => {
    let disposed = false;
    let cleanup = () => {};
    void apiPromise
      .then((api) => {
        if (disposed) return;
        const activity = api.events.on("activity.changed", () => {
          void queryClient.invalidateQueries({
            queryKey: activityQueryKeys.all,
            predicate: (query) => refreshesOnActivityChange(query.queryKey),
          });
        });
        const connection = observeActivityConnection(api.events, setConnectionState, () => {
          void queryClient.invalidateQueries({}, { cancelRefetch: false });
        });
        cleanup = () => {
          activity();
          connection();
        };
      })
      .catch(() => {});
    return () => {
      disposed = true;
      cleanup();
    };
  }, [apiPromise, queryClient]);
  const query = useQuery({
    queryKey: activityQueryKeys.sessions,
    retry: (failureCount, error) =>
      failureCount < 2 && error instanceof ApiError && error.kind === "connection",
    retryDelay: (attempt) => Math.min(5_000, 500 * 2 ** attempt),
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
  });

  // A day delete can remove sessions older than the recent page the refresh rereads.
  const reloadAllSessions = useCallback(async () => {
    const api = await apiPromise;
    const [health, sessions] = await Promise.all([
      api.health.get(),
      api.activity.listAllAppSessions(),
    ]);
    await queryClient.cancelQueries({ queryKey: activityQueryKeys.sessions, exact: true });
    queryClient.setQueryData<ActivityDataSnapshot>(activityQueryKeys.sessions, {
      sessions,
      health,
    });
  }, [apiPromise, queryClient]);
  const adjustLockedRuns = useCallback(
    (levelSessionId: string, lockedDelta: number, lockedClearDelta: number) => {
      queryClient.setQueryData<ActivityDataSnapshot>(activityQueryKeys.sessions, (current) =>
        current
          ? {
              ...current,
              sessions: adjustLockedRunCounts(
                current.sessions,
                levelSessionId,
                lockedDelta,
                lockedClearDelta,
              ),
            }
          : current,
      );
    },
    [queryClient],
  );

  const status =
    connectionState.session === "incompatible"
      ? "incompatible"
      : connectionState.session === "disconnected" || connectionState.session === "closed"
        ? "error"
        : connectionState.recorder === "error"
          ? "error"
          : connectionState.session === "reconnecting" ||
              connectionState.recorder === "unavailable" ||
              connectionState.recorder === "initializing"
            ? "connecting"
            : statusForQuery(query.status, query.error);
  return {
    sessions: query.data?.sessions ?? [],
    health: status === "online" && query.status === "success" ? query.data.health : null,
    status,
    error: diagnosticErrorMessage(query.error),
    versionMismatch: query.error instanceof IpcProtocolMismatchError ? query.error.direction : null,
    retry: query.refetch,
    reloadAllSessions,
    adjustLockedRuns,
    mockEnabled,
  };
}

function statusForQuery(status: "pending" | "error" | "success", cause: unknown): ConnectionStatus {
  if (status === "pending") return "connecting";
  if (status === "success") return "online";
  return connectionStatusForError(cause);
}

export function connectionStatusForError(cause: unknown): ConnectionStatus {
  return cause instanceof IpcProtocolMismatchError ||
    (cause instanceof ApiError && cause.kind === "protocol")
    ? "incompatible"
    : "error";
}
