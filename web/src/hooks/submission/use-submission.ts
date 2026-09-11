import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useApiPromise } from "@/api/app-api-provider";
import { submissionKeys } from "@/state/submission/submission-queries";

export function useSubmissionSettings(enabled = true) {
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
    onSettled: () => {
      cache.removeQueries({ queryKey: submissionKeys.all });
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
  const api = useApiPromise();
  const cache = useQueryClient();
  const status = useQuery({
    queryKey: submissionKeys.status,
    queryFn: async () => (await api).submission.status(),
    enabled,
    staleTime: 2000,
    retry: false,
  });
  const run = useQuery({
    queryKey: submissionKeys.run(id ?? "missing"),
    queryFn: async () => (await api).submission.get(id as string),
    enabled: enabled && id !== null && status.data?.connected === true,
    retry: false,
  });
  const submit = useMutation({
    mutationFn: async () => (await api).submission.submit(id as string),
    onSuccess: (value) => cache.setQueryData(submissionKeys.run(value.run_id), value),
  });
  return { status, run, submit };
}
