import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useApiPromise } from "@/api/app-api-provider";
import {
  type DownloadAction,
  type DownloadItemId,
  installationBusy,
} from "@/api/downloads/downloads-api";
import { ApiError } from "@/shared/errors/api-error";

export const downloadsQueryKey = ["downloads", "status"] as const;
export function useDownloads(enabled: boolean) {
  const promise = useApiPromise();
  const queryClient = useQueryClient();
  async function api() {
    const downloads = (await promise).downloads;
    if (!downloads)
      throw new ApiError("Update TUFReplay and restart ADOFAI to use the download center.", {
        kind: "domain",
        code: "downloads_unavailable",
      });
    return downloads;
  }
  const query = useQuery({
    queryKey: downloadsQueryKey,
    queryFn: async () => (await api()).getStatus(),
    enabled,
    refetchInterval: (query) =>
      installationBusy(query.state.data?.Ffmpeg) || installationBusy(query.state.data?.Renderer)
        ? 500
        : 2000,
    staleTime: 500,
    retry: false,
  });
  const mutation = useMutation({
    scope: { id: "download-center" },
    onMutate: () => queryClient.cancelQueries({ queryKey: downloadsQueryKey }),
    mutationFn: async ({ item, action }: { item: DownloadItemId; action: DownloadAction }) =>
      (await api()).act(item, action),
    onSuccess: (state) => queryClient.setQueryData(downloadsQueryKey, state),
  });
  return {
    state: query.data,
    loading: query.isPending,
    error: mutation.error ?? query.error,
    actionFailed: Boolean(mutation.error),
    pending: mutation.isPending,
    act: (item: DownloadItemId, action: DownloadAction) => {
      mutation.reset();
      mutation.mutate({ item, action });
    },
    refresh: () => {
      mutation.reset();
      void query.refetch();
    },
  };
}
