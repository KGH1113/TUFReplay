import { type RefObject, useCallback, useEffect, useRef, useState } from "react";

import i18n from "../../i18n/i18n";
import type {
  ConnectionStatus,
  ReplayLevelFilePickerResult,
  ReplayStatus,
} from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { localizedErrorMessage } from "../lib/localized-error";
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
  const [pickerResult, setPickerResult] = useState<ReplayLevelFilePickerResult | null>(null);
  const [pickingRunId, setPickingRunId] = useState<string | null>(null);
  const statusRef = useRef(status);
  const pendingRunIdRef = useRef(pendingRunId);
  const playGenerationRef = useRef(0);
  const pickerGenerationRef = useRef(0);
  const mountedRef = useRef(true);
  statusRef.current = status;
  pendingRunIdRef.current = pendingRunId;

  const pollerRef = useRef<ReplayStatusPoller | null>(null);
  if (!pollerRef.current) {
    pollerRef.current = new ReplayStatusPoller(
      async () => {
        const gateway = gatewayRef.current;
        if (!gateway) throw new Error(i18n.t("errors.notConnected", { ns: "common" }));
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

  const play = useCallback(
    async (runId: string, levelPath?: string) => {
      const gateway = gatewayRef.current;
      if (!gateway) {
        setError(i18n.t("errors.notConnected", { ns: "common" }));
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

  const pickLevelFile = useCallback(
    async (runId: string) => {
      const gateway = gatewayRef.current;
      if (!gateway) {
        setPickerResult({
          OperationId: null,
          RunId: runId,
          Outcome: "error",
          LevelPath: null,
          ErrorCode: "not_connected",
          Message: i18n.t("errors.notConnected", { ns: "common" }),
        });
        return false;
      }

      const generation = pickerGenerationRef.current + 1;
      pickerGenerationRef.current = generation;
      setPickerResult(null);
      setPickingRunId(runId);
      try {
        const next = await gateway.pickReplayLevelFile(runId);
        if (generation !== pickerGenerationRef.current) return false;
        setPickerResult(next);
        return true;
      } catch (cause) {
        if (generation === pickerGenerationRef.current) {
          setPickerResult({
            OperationId: null,
            RunId: runId,
            Outcome: "error",
            LevelPath: null,
            ErrorCode: "file_picker_failed",
            Message: errorMessage(cause),
          });
        }
        return false;
      } finally {
        if (generation === pickerGenerationRef.current) setPickingRunId(null);
      }
    },
    [gatewayRef],
  );

  const clearLevelFilePicker = useCallback(() => {
    pickerGenerationRef.current += 1;
    setPickerResult(null);
    setPickingRunId(null);
  }, []);

  return {
    status,
    pendingRunId,
    error,
    errorRunId,
    pickerResult,
    pickingRunId,
    play,
    pickLevelFile,
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

function errorMessage(cause: unknown) {
  return localizedErrorMessage(cause, i18n.t("controlFailed", { ns: "replay" }));
}
