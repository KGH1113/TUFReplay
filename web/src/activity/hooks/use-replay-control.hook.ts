import { type RefObject, useCallback, useEffect, useRef, useState } from "react";

import type {
  ConnectionStatus,
  ReplayLevelFilePickerStatus,
  ReplayStatus,
} from "../activity.model";
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

export function useReplayControl(
  gatewayRef: RefObject<ActivityGateway | null>,
  connectionStatus: ConnectionStatus,
) {
  const [status, setStatus] = useState<ReplayStatus>(IDLE_STATUS);
  const [pendingRunId, setPendingRunId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [errorRunId, setErrorRunId] = useState<string | null>(null);
  const [pickerStatus, setPickerStatus] = useState<ReplayLevelFilePickerStatus | null>(null);
  const statusRef = useRef(status);
  const pendingRunIdRef = useRef(pendingRunId);
  const playGenerationRef = useRef(0);
  const pickerGenerationRef = useRef(0);
  const pickerRefreshInFlightRef = useRef(false);
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
    return () => {
      mountedRef.current = false;
    };
  }, []);
  useEffect(() => {
    if (connectionStatus === "online") void pollerRef.current?.refresh();
  }, [connectionStatus]);

  useVisiblePolling(() => {
    if (pendingRunIdRef.current || !shouldPollReplayStatus(statusRef.current)) return;
    void pollerRef.current?.refresh();
  }, POLL_INTERVAL_MS);

  useVisiblePolling(() => {
    const current = pickerStatus;
    if (!current || !shouldPollReplayLevelFilePicker(current) || pickerRefreshInFlightRef.current)
      return;

    const gateway = gatewayRef.current;
    if (!gateway) return;
    const generation = pickerGenerationRef.current;
    pickerRefreshInFlightRef.current = true;
    void gateway
      .getReplayLevelFilePickerStatus(current.OperationId)
      .then((next) => {
        if (mountedRef.current && generation === pickerGenerationRef.current) setPickerStatus(next);
      })
      .catch((cause) => {
        if (mountedRef.current && generation === pickerGenerationRef.current) {
          setPickerStatus({
            ...current,
            State: "error",
            ErrorCode: "file_picker_status_failed",
            Message: errorMessage(cause),
          });
        }
      })
      .finally(() => {
        pickerRefreshInFlightRef.current = false;
      });
  }, POLL_INTERVAL_MS);

  const play = useCallback(
    async (runId: string, levelPath?: string) => {
      const gateway = gatewayRef.current;
      if (!gateway) {
        setError("TUFReplay is not connected");
        return false;
      }

      const generation = playGenerationRef.current + 1;
      playGenerationRef.current = generation;
      pollerRef.current?.invalidate();
      pendingRunIdRef.current = runId;
      setPendingRunId(runId);
      setError("");
      setErrorRunId(null);
      try {
        const next = await gateway.playReplay(runId, levelPath);
        if (generation !== playGenerationRef.current) return false;
        statusRef.current = next;
        setStatus(next);
        return true;
      } catch (cause) {
        if (generation === playGenerationRef.current) {
          setError(errorMessage(cause));
          setErrorRunId(runId);
        }
        return false;
      } finally {
        if (generation === playGenerationRef.current) {
          pendingRunIdRef.current = null;
          setPendingRunId(null);
        }
      }
    },
    [gatewayRef],
  );

  const startLevelFilePicker = useCallback(
    async (runId: string) => {
      const gateway = gatewayRef.current;
      if (!gateway) {
        setPickerStatus({
          OperationId: "",
          RunId: runId,
          State: "error",
          LevelPath: null,
          ErrorCode: "not_connected",
          Message: "TUFReplay is not connected",
        });
        return false;
      }

      const generation = pickerGenerationRef.current + 1;
      pickerGenerationRef.current = generation;
      setPickerStatus(null);
      try {
        const next = await gateway.startReplayLevelFilePicker(runId);
        if (generation !== pickerGenerationRef.current) return false;
        setPickerStatus(next);
        return true;
      } catch (cause) {
        if (generation === pickerGenerationRef.current) {
          setPickerStatus({
            OperationId: "",
            RunId: runId,
            State: "error",
            LevelPath: null,
            ErrorCode: "file_picker_start_failed",
            Message: errorMessage(cause),
          });
        }
        return false;
      }
    },
    [gatewayRef],
  );

  const clearLevelFilePicker = useCallback(() => {
    pickerGenerationRef.current += 1;
    pickerRefreshInFlightRef.current = false;
    setPickerStatus(null);
  }, []);

  return {
    status,
    pendingRunId,
    error,
    errorRunId,
    pickerStatus,
    play,
    startLevelFilePicker,
    clearLevelFilePicker,
  };
}

export function shouldPollReplayStatus(status: ReplayStatus) {
  return (
    status.State === "preparing" ||
    status.State === "opening_level" ||
    status.State === "waiting_for_focus" ||
    status.State === "starting" ||
    status.State === "playing" ||
    status.State === "returning_to_editor"
  );
}

export function shouldPollReplayLevelFilePicker(status: ReplayLevelFilePickerStatus) {
  return status.State === "picking";
}

function errorMessage(cause: unknown) {
  return cause instanceof Error ? cause.message : "Could not control replay";
}
