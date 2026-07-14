import { useCallback, useRef, useState } from "react";

import type { ActivityAppSession, ConnectionStatus } from "../activity.model";
import { connectActivityGateway, type ActivityGateway } from "../data/activity.gateway";
import { createMockActivityGateway } from "../mock/activity.mock";
import { useVisiblePolling } from "./use-visible-polling.hook";

const mockActivityEnabled = import.meta.env.DEV && (
  import.meta.env.VITE_USE_MOCK_ACTIVITY === "true"
  || typeof window !== "undefined" && new URLSearchParams(window.location.search).get("mock") === "1"
);

export function useActivityData() {
  const gatewayRef = useRef<ActivityGateway | null>(null);
  if (gatewayRef.current === null && mockActivityEnabled) gatewayRef.current = createMockActivityGateway();
  const loadingRef = useRef(false);
  const [sessions, setSessions] = useState<ActivityAppSession[]>([]);
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const [error, setError] = useState("");

  const refresh = useCallback(async () => {
    if (loadingRef.current) return;
    loadingRef.current = true;
    try {
      const gateway = gatewayRef.current ?? await connectActivityGateway();
      gatewayRef.current = gateway;
      await gateway.health();
      const next = await gateway.listAllAppSessions(setSessions);
      setSessions(next);
      setStatus("online");
      setError("");
    } catch (cause) {
      gatewayRef.current = null;
      setStatus("error");
      setError(cause instanceof Error ? cause.message : "Could not connect to TUFReplay");
    } finally {
      loadingRef.current = false;
    }
  }, []);

  useVisiblePolling(() => void refresh());
  return { sessions, status, error, retry: refresh, gatewayRef };
}
