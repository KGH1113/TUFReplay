import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import i18n from "@/i18n/i18n";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import type { WebcamSettingsPatch } from "@/models/webcam/webcam-model";
import { webcamQueryKeys } from "@/state/webcam/webcam-queries";

export function useWebcamSettings(connectionStatus: ConnectionStatus) {
  const [open, setOpen] = useState(false);
  const forceDeviceRefresh = useRef(false);
  const apiPromise = useApiPromise();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: webcamQueryKeys.settings,
    queryFn: async () => {
      const refresh = forceDeviceRefresh.current;
      forceDeviceRefresh.current = false;
      return (await apiPromise).webcam.getSettings(refresh);
    },
    enabled: open && connectionStatus === "online",
    staleTime: 5000,
    retry: 1,
  });
  useEffect(() => {
    let disposed = false;
    let off = () => {};
    void apiPromise
      .then((api) => {
        if (!disposed)
          off = api.events.on("webcam.changed", (state) =>
            queryClient.setQueryData(webcamQueryKeys.settings, state),
          );
      })
      .catch(() => {});
    return () => {
      disposed = true;
      off();
    };
  }, [apiPromise, queryClient]);
  const mutation = useMutation({
    scope: { id: "camera-settings" },
    mutationFn: async (patch: WebcamSettingsPatch) =>
      (await apiPromise).webcam.updateSettings(patch),
    onMutate: () => queryClient.cancelQueries({ queryKey: webcamQueryKeys.settings }),
    onSuccess: (state) => queryClient.setQueryData(webcamQueryKeys.settings, state),
  });
  const error = mutation.error ?? query.error;
  return {
    open,
    setOpen,
    state: query.data,
    loading: query.isPending,
    saving: mutation.isPending,
    saved: mutation.isSuccess,
    error: error
      ? localizedErrorMessage(error, i18n.t("errors.readSettings", { ns: "webcam" }))
      : "",
    refresh: () => {
      mutation.reset();
      forceDeviceRefresh.current = true;
      void query.refetch();
    },
    update: (patch: WebcamSettingsPatch) => {
      mutation.reset();
      mutation.mutate(patch);
    },
  };
}
