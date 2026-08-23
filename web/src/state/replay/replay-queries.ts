import type { QueryClient } from "@tanstack/react-query";

import type { ReplayStatus } from "@/models/replay/replay-model";

export const replayQueryKeys = {
  all: ["replay"] as const,
  status: ["replay", "status"] as const,
};

export function isReplayActive(status: ReplayStatus | undefined) {
  return (
    status?.state === "preparing" ||
    status?.state === "opening_level" ||
    status?.state === "waiting_for_focus" ||
    status?.state === "starting" ||
    status?.state === "playing" ||
    status?.state === "returning_to_editor"
  );
}

export function setReplayStatus(queryClient: QueryClient, status: ReplayStatus) {
  queryClient.setQueryData(replayQueryKeys.status, status);
}
