import type { QueryClient } from "@tanstack/react-query";

import { isReplayInProgress, type ReplayStatus } from "@/models/replay/replay-model";

export const replayQueryKeys = {
  all: ["replay"] as const,
  status: ["replay", "status"] as const,
};

export function isReplayActive(status: ReplayStatus | undefined) {
  return isReplayInProgress(status);
}

export function setReplayStatus(queryClient: QueryClient, status: ReplayStatus) {
  queryClient.setQueryData(replayQueryKeys.status, status);
}
