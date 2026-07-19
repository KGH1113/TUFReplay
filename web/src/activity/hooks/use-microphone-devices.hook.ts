import { type RefObject, useCallback, useEffect, useRef, useState } from "react";

import type { ConnectionStatus, MicrophoneDevice, MicrophoneDevicesState } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";

const EMPTY_STATE: MicrophoneDevicesState = {
  Devices: [],
  SelectedDeviceId: null,
};

export function useMicrophoneDevices(
  gatewayRef: RefObject<ActivityGateway | null>,
  connectionStatus: ConnectionStatus,
) {
  const [state, setState] = useState(EMPTY_STATE);
  const [loading, setLoading] = useState(false);
  const [pendingDeviceId, setPendingDeviceId] = useState<string | null | undefined>(undefined);
  const [error, setError] = useState("");
  const refreshInFlightRef = useRef(false);

  const applyState = useCallback((next: MicrophoneDevicesState) => {
    setState({
      Devices: Array.isArray(next.Devices) ? next.Devices : [],
      SelectedDeviceId: next.SelectedDeviceId ?? null,
    });
    setError("");
  }, []);

  const refresh = useCallback(async () => {
    if (connectionStatus !== "online" || refreshInFlightRef.current) return;
    const gateway = gatewayRef.current;
    if (!gateway) return;

    refreshInFlightRef.current = true;
    setLoading(true);
    try {
      applyState(await gateway.getMicrophoneDevices());
    } catch (cause) {
      setError(errorMessage(cause));
    } finally {
      refreshInFlightRef.current = false;
      setLoading(false);
    }
  }, [applyState, connectionStatus, gatewayRef]);

  const select = useCallback(
    async (deviceId: string | null) => {
      const gateway = gatewayRef.current;
      if (!gateway || connectionStatus !== "online" || pendingDeviceId !== undefined) return;

      setPendingDeviceId(deviceId);
      setError("");
      try {
        applyState(await gateway.selectMicrophoneDevice(deviceId));
      } catch (cause) {
        setError(errorMessage(cause));
      } finally {
        setPendingDeviceId(undefined);
      }
    },
    [applyState, connectionStatus, gatewayRef, pendingDeviceId],
  );

  useEffect(() => {
    if (connectionStatus === "online") {
      void refresh();
      return;
    }
    setLoading(false);
    if (connectionStatus === "error") setError("TUFReplay is not connected");
  }, [connectionStatus, refresh]);

  return {
    devices: state.Devices as MicrophoneDevice[],
    selectedDeviceId: state.SelectedDeviceId,
    loading,
    pendingDeviceId,
    error,
    refresh,
    select,
  };
}

function errorMessage(cause: unknown) {
  return cause instanceof Error ? cause.message : "Could not read microphone devices";
}
