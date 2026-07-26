import { type RefObject, useCallback, useEffect, useRef, useState } from "react";

import type { ConnectionStatus, MicrophoneDevice, MicrophoneDevicesState } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";

const EMPTY_STATE: MicrophoneDevicesState = {
  Enabled: true,
  ToggleLocked: false,
  Devices: [],
  SelectedDeviceId: null,
};

const DEVICE_REFRESH_MAX_AGE_MS = 5_000;

export function useMicrophoneDevices(
  gatewayRef: RefObject<ActivityGateway | null>,
  connectionStatus: ConnectionStatus,
) {
  const [state, setState] = useState(EMPTY_STATE);
  const [loading, setLoading] = useState(false);
  const [pendingDeviceId, setPendingDeviceId] = useState<string | null | undefined>(undefined);
  const [pendingEnabled, setPendingEnabled] = useState<boolean | undefined>(undefined);
  const [error, setError] = useState("");
  const refreshInFlightRef = useRef(false);
  const loadedRef = useRef(false);
  const lastRefreshAtRef = useRef(0);

  const applyState = useCallback((next: MicrophoneDevicesState) => {
    const normalized = {
      Enabled: next.Enabled !== false,
      ToggleLocked: next.ToggleLocked === true,
      Devices: Array.isArray(next.Devices) ? next.Devices : [],
      SelectedDeviceId: next.SelectedDeviceId ?? null,
    };
    setState((current) => (microphoneStatesEqual(current, normalized) ? current : normalized));
    setError((current) => (current ? "" : current));
  }, []);

  const refresh = useCallback(async () => {
    if (connectionStatus !== "online" || refreshInFlightRef.current) return;
    const gateway = gatewayRef.current;
    if (!gateway) return;

    refreshInFlightRef.current = true;
    const showLoading = !loadedRef.current;
    if (showLoading) setLoading(true);
    try {
      applyState(await gateway.getMicrophoneDevices());
      loadedRef.current = true;
      lastRefreshAtRef.current = Date.now();
    } catch (cause) {
      setError(errorMessage(cause));
    } finally {
      refreshInFlightRef.current = false;
      if (showLoading) setLoading(false);
    }
  }, [applyState, connectionStatus, gatewayRef]);

  const refreshIfStale = useCallback(() => {
    if (Date.now() - lastRefreshAtRef.current < DEVICE_REFRESH_MAX_AGE_MS) return;
    void refresh();
  }, [refresh]);

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

  const setEnabled = useCallback(
    async (enabled: boolean) => {
      const gateway = gatewayRef.current;
      if (
        !gateway ||
        connectionStatus !== "online" ||
        pendingEnabled !== undefined ||
        pendingDeviceId !== undefined
      )
        return;

      setPendingEnabled(enabled);
      setError("");
      try {
        applyState(await gateway.setMicrophoneEnabled(enabled));
      } catch (cause) {
        setError(errorMessage(cause));
      } finally {
        setPendingEnabled(undefined);
      }
    },
    [applyState, connectionStatus, gatewayRef, pendingDeviceId, pendingEnabled],
  );

  useEffect(() => {
    if (connectionStatus === "online") {
      void refresh();
      return;
    }
    setLoading(false);
    loadedRef.current = false;
    lastRefreshAtRef.current = 0;
    if (connectionStatus === "error") setError("TUFReplay is not connected");
  }, [connectionStatus, refresh]);

  return {
    devices: state.Devices as MicrophoneDevice[],
    enabled: state.Enabled,
    toggleLocked: state.ToggleLocked,
    selectedDeviceId: state.SelectedDeviceId,
    loading,
    pendingDeviceId,
    pendingEnabled,
    error,
    refresh,
    refreshIfStale,
    select,
    setEnabled,
  };
}

function microphoneStatesEqual(left: MicrophoneDevicesState, right: MicrophoneDevicesState) {
  if (
    left.Enabled !== right.Enabled ||
    left.ToggleLocked !== right.ToggleLocked ||
    left.SelectedDeviceId !== right.SelectedDeviceId ||
    left.Devices.length !== right.Devices.length
  )
    return false;
  return left.Devices.every((device, index) => {
    const other = right.Devices[index];
    return (
      device.Id === other.Id &&
      device.Name === other.Name &&
      device.MinFrequency === other.MinFrequency &&
      device.MaxFrequency === other.MaxFrequency
    );
  });
}

function errorMessage(cause: unknown) {
  return cause instanceof Error ? cause.message : "Could not read microphone devices";
}
