import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useRef, useState } from "react";

import { useApiPromise } from "@/api/app-api-provider";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import type { ReplayLevelFilePickerResult, ReplayStatus } from "@/models/replay/replay-model";
import { isReplayActive, replayQueryKeys, setReplayStatus } from "@/state/replay/replay-queries";

const IDLE_STATUS: ReplayStatus = {
  operationId: null,
  runId: null,
  state: "idle",
  errorCode: null,
  message: null,
};

const POLL_INTERVAL_MS = 500;

export function useReplayControl(connectionStatus: ConnectionStatus) {
  const apiPromise = useApiPromise();
  const queryClient = useQueryClient();
  const playGeneration = useRef(0);
  const pickerGeneration = useRef(0);
  const [pickerResult, setPickerResult] = useState<ReplayLevelFilePickerResult | null>(null);

  const statusQuery = useQuery({
    queryKey: replayQueryKeys.status,
    queryFn: async () => (await apiPromise).replay.getStatus(),
    enabled: connectionStatus === "online",
    refetchInterval: (query) => (isReplayActive(query.state.data) ? POLL_INTERVAL_MS : false),
    refetchIntervalInBackground: false,
  });

  const playMutation = useMutation({
    mutationFn: async ({ runId, levelPath }: { runId: string; levelPath?: string }) =>
      (await apiPromise).replay.play(runId, levelPath),
  });
  const pickerMutation = useMutation({
    mutationFn: async (runId: string) => (await apiPromise).replay.pickLevelFile(runId),
  });

  const play = useCallback(
    async (runId: string, levelPath?: string) => {
      const generation = ++playGeneration.current;
      try {
        const next = await playMutation.mutateAsync({ runId, levelPath });
        if (generation !== playGeneration.current) return false;
        setReplayStatus(queryClient, next);
        return true;
      } catch {
        return false;
      }
    },
    [playMutation, queryClient],
  );

  const pickLevelFile = useCallback(
    async (runId: string) => {
      const generation = ++pickerGeneration.current;
      setPickerResult(null);
      try {
        const next = await pickerMutation.mutateAsync(runId);
        if (generation !== pickerGeneration.current) return false;
        setPickerResult(next);
        return true;
      } catch (cause) {
        if (generation === pickerGeneration.current) {
          setPickerResult({
            operationId: null,
            runId,
            outcome: "error",
            levelPath: null,
            errorCode: "file_picker_failed",
            message: cause instanceof Error ? cause.message : String(cause),
          });
        }
        return false;
      }
    },
    [pickerMutation],
  );

  const clearLevelFilePicker = useCallback(() => {
    pickerGeneration.current += 1;
    setPickerResult(null);
    pickerMutation.reset();
  }, [pickerMutation]);

  const status = statusQuery.data ?? IDLE_STATUS;
  const errorCause = playMutation.error ?? statusQuery.error;
  return {
    status,
    pendingRunId: playMutation.isPending ? (playMutation.variables?.runId ?? null) : null,
    error: errorCause instanceof Error ? errorCause.message : "",
    errorRunId: playMutation.isError ? (playMutation.variables?.runId ?? null) : null,
    pickerResult,
    pickingRunId: pickerMutation.isPending ? (pickerMutation.variables ?? null) : null,
    play,
    pickLevelFile,
    clearLevelFilePicker,
  };
}
