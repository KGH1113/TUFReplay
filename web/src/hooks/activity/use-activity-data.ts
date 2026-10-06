import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useApiPromise, useMockEnabled } from "@/api/app-api-provider";
import type { AppSession, ConnectionStatus } from "@/models/activity/activity-model";
import { diagnosticErrorMessage } from "@/models/activity/localized-error";
import type { Health } from "@/models/health/health-model";
import type { SessionState } from "@/ports/local-message-peer";
import { ApiError } from "@/shared/errors/api-error";
import { IpcProtocolMismatchError } from "@/shared/errors/ipc-protocol-mismatch-error";
import {
  activityQueryKeys,
  mergeRecentAppSessions,
  RECENT_ACTIVITY_SESSION_LIMIT,
} from "@/state/activity/activity-queries";

interface ActivityDataSnapshot {
  sessions: AppSession[];
  health: Health;
}

export function useActivityData() {
  const apiPromise = useApiPromise();
  const mockEnabled = useMockEnabled();
  const queryClient = useQueryClient();
  const [sessionState, setSessionState] = useState<SessionState | null>(null);
  useEffect(() => {
    let disposed = false;
    let cleanup = () => {};
    void apiPromise
      .then((api) => {
        if (disposed) return;
        const activity = api.events.on("activity.changed", () => {
          void queryClient.invalidateQueries({ queryKey: activityQueryKeys.all });
        });
        const connection = api.events.on("connection.changed", (state) => {
          setSessionState(state);
          if (state === "connected") void queryClient.invalidateQueries();
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

  return {
    sessions: query.data?.sessions ?? [],
    health: query.status === "success" ? query.data.health : null,
    status:
      sessionState === "incompatible"
        ? "incompatible"
        : sessionState === "reconnecting"
          ? "connecting"
          : sessionState === "disconnected" || sessionState === "closed"
            ? "error"
            : statusForQuery(query.status, query.error),
    error: diagnosticErrorMessage(query.error),
    versionMismatch: query.error instanceof IpcProtocolMismatchError ? query.error.direction : null,
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
  return cause instanceof IpcProtocolMismatchError ||
    (cause instanceof ApiError && cause.kind === "protocol")
    ? "incompatible"
    : "error";
}
