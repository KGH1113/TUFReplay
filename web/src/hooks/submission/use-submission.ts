import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useApiPromise } from "@/api/app-api-provider";
import type { VisualSelection } from "@/models/submission/submission-model";
import { hasSubmissionPermission } from "@/models/submission/submission-model";
import { isSubmissionProcessing } from "@/models/submission/submission-progress";
import { TUFREPLAY_WEB_BUILD } from "@/shared/config/tufreplay-build-info";
import { submissionKeys } from "@/state/submission/submission-queries";
import { visualKeys } from "@/state/visual/visual-queries";

export function useSubmissionSettings(enabled = true) {
  enabled = enabled && TUFREPLAY_WEB_BUILD.flavor === "auto-submission";
  const api = useApiPromise();
  const cache = useQueryClient();
  const status = useQuery({
    queryKey: submissionKeys.status,
    queryFn: async () => (await api).submission.status(),
    enabled,
    refetchInterval: enabled ? 2000 : false,
    retry: false,
  });
  const connect = useMutation({
    mutationFn: async () => (await api).submission.connect(),
    onSuccess: (value) => {
      cache.setQueryData(submissionKeys.status, value);
    },
  });
  const disconnect = useMutation({
    mutationFn: async () => (await api).submission.disconnect(),
    onSuccess: (value) => {
      cache.setQueryData(submissionKeys.status, value);
    },
    onSettled: () => {
      cache.removeQueries({ queryKey: submissionKeys.all });
      void cache
        .cancelQueries({ queryKey: visualKeys.all })
        .then(() => cache.removeQueries({ queryKey: visualKeys.all }));
      void cache.invalidateQueries({ queryKey: submissionKeys.status });
    },
  });
  const setDisabled = useMutation({
    mutationFn: async (disabled: boolean) => (await api).submission.setDisabled(disabled),
    onSuccess: (value) => cache.setQueryData(submissionKeys.status, value),
  });
  return { status, connect, disconnect, setDisabled };
}

export function useSubmissionRun(id: string | null, enabled: boolean) {
  enabled = enabled && TUFREPLAY_WEB_BUILD.flavor === "auto-submission";
  const api = useApiPromise();
  const cache = useQueryClient();
  const status = useQuery({
    queryKey: submissionKeys.status,
    queryFn: async () => (await api).submission.status(),
    enabled,
    staleTime: 2000,
    refetchInterval: enabled ? 2000 : false,
    retry: false,
  });
  const run = useQuery({
    queryKey: submissionKeys.run(id ?? "missing"),
    queryFn: async () => (await api).submission.get(id as string),
    enabled: enabled && id !== null && status.data?.connected === true,
    retry: false,
    refetchInterval: (query) =>
      enabled &&
      (!query.state.data ||
        query.state.status === "error" ||
        isSubmissionProcessing(query.state.data.status))
        ? 2000
        : false,
  });
  const submit = useMutation({
    mutationFn: async (presentation?: VisualSelection) => {
      if (!enabled || id === null) throw new Error("submission_not_authorized");
      const submissionApi = (await api).submission;
      const latestStatus = await cache.fetchQuery({
        queryKey: submissionKeys.status,
        queryFn: () => submissionApi.status(),
        staleTime: 0,
        retry: false,
      });
      if (!hasSubmissionPermission(latestStatus)) throw new Error("submission_not_authorized");
      try {
        return await submissionApi.submit(id, presentation);
      } catch (cause) {
        // A response can be lost after the server fixes the presentation. Refresh the
        // run before surfacing the error so the next retry omits the selection body.
        try {
          await cache.fetchQuery({
            queryKey: submissionKeys.run(id),
            queryFn: () => submissionApi.get(id),
            staleTime: 0,
            retry: false,
          });
        } catch {
          // Keep the original submit error; a second fetch may fail for the same reason.
        }
        throw cause;
      }
    },
    onSuccess: (value) => cache.setQueryData(submissionKeys.run(value.run_id), value),
  });
  return { status, run, submit };
}
