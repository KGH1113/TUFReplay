import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import {
  acknowledgeLegacyReplayNotice,
  hasAcknowledgedLegacyReplayNotice,
} from "@/state/activity/legacy-replay-notice";

const LEGACY_REPLAY_STATUS_QUERY_KEY = ["activity", "legacy-replay-status"] as const;

export function useLegacyReplayNotice(connectionStatus: ConnectionStatus) {
  const apiPromise = useApiPromise();
  const [acknowledged, setAcknowledged] = useState(hasAcknowledgedLegacyReplayNotice);
  const query = useQuery({
    queryKey: LEGACY_REPLAY_STATUS_QUERY_KEY,
    queryFn: async () => (await apiPromise).activity.getLegacyReplayStatus(),
    enabled: connectionStatus === "online" && !acknowledged,
    staleTime: Number.POSITIVE_INFINITY,
  });

  return {
    open: !acknowledged && query.data?.hasLegacyReplays === true,
    confirm() {
      acknowledgeLegacyReplayNotice();
      setAcknowledged(true);
    },
  };
}
