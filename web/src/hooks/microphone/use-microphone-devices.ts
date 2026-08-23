import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";

import { useApiPromise } from "@/api/app-api-provider";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import type {
  MicrophoneDevicesState,
  MicrophoneTimingSettings,
} from "@/models/microphone/microphone-model";
import { microphoneQueryKeys } from "@/state/microphone/microphone-queries";

const EMPTY_STATE: MicrophoneDevicesState = {
  enabled: false,
  toggleLocked: false,
  devices: [],
  selectedDeviceId: null,
  microphoneOffsetMs: 0,
  microphoneVolumeDb: 0,
};
const DEVICE_REFRESH_MAX_AGE_MS = 5000;

export function useMicrophoneDevices(connectionStatus: ConnectionStatus) {
  const apiPromise = useApiPromise();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: microphoneQueryKeys.devices,
    queryFn: async () => (await apiPromise).microphone.getDevices(),
    enabled: connectionStatus === "online",
    staleTime: DEVICE_REFRESH_MAX_AGE_MS,
  });
  const enabledMutation = useMutation({
    mutationFn: async (enabled: boolean) => (await apiPromise).microphone.setEnabled(enabled),
    onSuccess: (next) => queryClient.setQueryData(microphoneQueryKeys.devices, next),
  });
  const deviceMutation = useMutation({
    mutationFn: async (deviceId: string | null) =>
      (await apiPromise).microphone.selectDevice(deviceId),
    onSuccess: (next) => queryClient.setQueryData(microphoneQueryKeys.devices, next),
  });

  const refreshIfStale = useCallback(() => {
    const state = queryClient.getQueryState(microphoneQueryKeys.devices);
    if (!state || Date.now() - state.dataUpdatedAt >= DEVICE_REFRESH_MAX_AGE_MS)
      void query.refetch();
  }, [query, queryClient]);
  const applyTimingSettings = useCallback(
    (settings: MicrophoneTimingSettings) => {
      queryClient.setQueryData<MicrophoneDevicesState>(microphoneQueryKeys.devices, (current) =>
        current
          ? {
              ...current,
              microphoneOffsetMs: settings.microphoneOffsetMs,
              microphoneVolumeDb: settings.microphoneVolumeDb,
            }
          : current,
      );
    },
    [queryClient],
  );

  const state = query.data ?? EMPTY_STATE;
  const error = query.error ?? enabledMutation.error ?? deviceMutation.error;
  return {
    ...state,
    loading: query.isPending,
    pendingDeviceId: deviceMutation.isPending ? deviceMutation.variables : undefined,
    pendingEnabled: enabledMutation.isPending ? enabledMutation.variables : undefined,
    error: error instanceof Error ? error.message : "",
    refreshIfStale,
    setEnabled: enabledMutation.mutateAsync,
    select: deviceMutation.mutateAsync,
    applyTimingSettings,
  };
}
