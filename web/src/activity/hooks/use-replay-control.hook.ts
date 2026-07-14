import { useCallback, useEffect, useRef, useState, type RefObject } from "react";

import type { ConnectionStatus, ReplayStatus } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { ReplayStatusPoller } from "../lib/replay-status.poller";
import { useVisiblePolling } from "./use-visible-polling.hook";

const IDLE_STATUS: ReplayStatus = {
  OperationId: null,
  RunId: null,
  State: "idle",
  ErrorCode: null,
  Message: null,
};

const POLL_INTERVAL_MS = 500;

export function useReplayControl(gatewayRef: RefObject<ActivityGateway | null>, connectionStatus: ConnectionStatus) {
  const [status, setStatus] = useState<ReplayStatus>(IDLE_STATUS);
  const [pendingRunId, setPendingRunId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [errorRunId, setErrorRunId] = useState<string | null>(null);
  const statusRef = useRef(status);
  const pendingRunIdRef = useRef(pendingRunId);
  const playGenerationRef = useRef(0);
  const mountedRef = useRef(true);
  statusRef.current = status;
  pendingRunIdRef.current = pendingRunId;

  const pollerRef = useRef<ReplayStatusPoller | null>(null);
  if (!pollerRef.current) {
    pollerRef.current = new ReplayStatusPoller(
      async () => {
        const gateway = gatewayRef.current;
        if (!gateway) throw new Error("TUFReplay is not connected");
        return gateway.getReplayStatus();
      },
      (next) => {
        if (!mountedRef.current) return;
        statusRef.current = next;
        setStatus(next);
        setError("");
        setErrorRunId(null);
      },
      (cause) => {
        if (!mountedRef.current) return;
        setError(errorMessage(cause));
        setErrorRunId(statusRef.current.RunId);
      },
    );
  }

  useEffect(() => {
    mountedRef.current = true;
    return () => { mountedRef.current = false; };
  }, []);
  useEffect(() => {
    if (connectionStatus === "online") void pollerRef.current?.refresh();
  }, [connectionStatus]);

  useVisiblePolling(() => {
    if (pendingRunIdRef.current || !shouldPollReplayStatus(statusRef.current)) return;
    void pollerRef.current?.refresh();
  }, POLL_INTERVAL_MS);

  const play = useCallback(async (runId: string) => {
    const gateway = gatewayRef.current;
    if (!gateway) {
      setError("TUFReplay is not connected");
      return;
    }

    const generation = playGenerationRef.current + 1;
    playGenerationRef.current = generation;
    pollerRef.current?.invalidate();
    pendingRunIdRef.current = runId;
    setPendingRunId(runId);
    setError("");
    setErrorRunId(null);
    try {
      const next = await gateway.playReplay(runId);
      if (generation !== playGenerationRef.current) return;
      statusRef.current = next;
      setStatus(next);
    } catch (cause) {
      if (generation === playGenerationRef.current) {
        setError(errorMessage(cause));
        setErrorRunId(runId);
      }
    } finally {
      if (generation === playGenerationRef.current) {
        pendingRunIdRef.current = null;
        setPendingRunId(null);
      }
    }
  }, [gatewayRef]);

  return { status, pendingRunId, error, errorRunId, play };
}

export function shouldPollReplayStatus(status: ReplayStatus) {
  return status.State === "preparing"
    || status.State === "opening_level"
    || status.State === "waiting_for_focus"
    || status.State === "starting"
    || status.State === "playing"
    || status.State === "returning_to_editor";
}

function errorMessage(cause: unknown) {
  return cause instanceof Error ? cause.message : "Could not control replay";
}
