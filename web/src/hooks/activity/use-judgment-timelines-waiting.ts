import { useIsFetching } from "@tanstack/react-query";
import { useEffect, useState } from "react";

const WAIT_HINT_DELAY_MS = 1_500;

/**
 * True when judgment timelines have been requested for a while without an answer. The mod only
 * holds them while ADOFAI is judging an attempt, so a slow answer means "after this attempt".
 */
export function useJudgmentTimelinesWaiting() {
  const fetching = useIsFetching({
    predicate: (query) =>
      query.queryKey[0] === "activity" && query.queryKey[3] === "judgment-timeline",
  });
  const [waiting, setWaiting] = useState(false);
  useEffect(() => {
    if (fetching === 0) {
      setWaiting(false);
      return;
    }
    const timer = setTimeout(() => setWaiting(true), WAIT_HINT_DELAY_MS);
    return () => clearTimeout(timer);
  }, [fetching]);
  return waiting;
}
