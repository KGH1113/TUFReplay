import { useCallback, useRef, useState } from "react";

import i18n from "../../i18n/i18n";
import type { ActivityAppSession, ConnectionStatus } from "../activity.model";
import {
  type ActivityGateway,
  ActivityProtocolMismatchError,
  connectActivityGateway,
} from "../data/activity.gateway";
import { localizedErrorMessage } from "../lib/localized-error";
import { createMockActivityGateway } from "../mock/activity.mock";
import { useVisiblePolling } from "./use-visible-polling.hook";

const mockActivityEnabled =
  import.meta.env.DEV &&
  (import.meta.env.VITE_USE_MOCK_ACTIVITY === "true" ||
    (typeof window !== "undefined" &&
      new URLSearchParams(window.location.search).get("mock") === "1"));

export const RECENT_ACTIVITY_SESSION_LIMIT = 20;

export function mergeRecentAppSessions(
  current: ActivityAppSession[],
  recent: ActivityAppSession[],
  limit = RECENT_ACTIVITY_SESSION_LIMIT,
): ActivityAppSession[] {
  if (recent.length < limit) return recent;

  const recentIds = new Set(recent.map((session) => session.Id));
  const boundary = recent[recent.length - 1];
  const older = current.filter(
    (session) =>
      !recentIds.has(session.Id) &&
      (session.StartedAtUtc < boundary.StartedAtUtc ||
        (session.StartedAtUtc === boundary.StartedAtUtc && session.Id < boundary.Id)),
  );

  return [...recent, ...older];
}

export function connectionStatusForError(cause: unknown): ConnectionStatus {
  return cause instanceof ActivityProtocolMismatchError ? "incompatible" : "error";
}

export function useActivityData() {
  const gatewayRef = useRef<ActivityGateway | null>(null);
  const hasCompleteHistoryRef = useRef(false);
  if (gatewayRef.current === null && mockActivityEnabled)
    gatewayRef.current = createMockActivityGateway();
  const loadingRef = useRef(false);
  const [sessions, setSessions] = useState<ActivityAppSession[]>([]);
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const [error, setError] = useState("");

  const refresh = useCallback(async () => {
    if (loadingRef.current) return;
    loadingRef.current = true;
    try {
      const gateway = gatewayRef.current ?? (await connectActivityGateway());
      await gateway.health();
      gatewayRef.current = gateway;
      setStatus("online");
      setError("");

      try {
        if (hasCompleteHistoryRef.current) {
          const recent = await gateway.listAppSessions(0, RECENT_ACTIVITY_SESSION_LIMIT);
          setSessions((current) => mergeRecentAppSessions(current, recent));
        } else {
          const next = await gateway.listAllAppSessions(setSessions);
          setSessions(next);
          hasCompleteHistoryRef.current = true;
        }
      } catch (cause) {
        setError(localizedErrorMessage(cause, i18n.t("errors.load", { ns: "activity" })));
      }
    } catch (cause) {
      gatewayRef.current = null;
      hasCompleteHistoryRef.current = false;
      setStatus(connectionStatusForError(cause));
      setError(localizedErrorMessage(cause, i18n.t("errors.connect", { ns: "activity" })));
    } finally {
      loadingRef.current = false;
    }
  }, []);

  const retry = useCallback(() => {
    hasCompleteHistoryRef.current = false;
    return refresh();
  }, [refresh]);

  useVisiblePolling(() => void refresh());
  return { sessions, status, error, retry, gatewayRef, mockEnabled: mockActivityEnabled };
}
