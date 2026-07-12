import { useEffect, useRef } from "react";

export function installVisiblePolling(refresh: () => void, intervalMs = 3000) {
  let timer: ReturnType<typeof setInterval> | undefined;
  const stop = () => {
    if (timer !== undefined) clearInterval(timer);
    timer = undefined;
  };
  const start = () => {
    stop();
    if (document.visibilityState !== "visible") return;
    refresh();
    timer = setInterval(refresh, intervalMs);
  };
  const onVisibility = () => (document.visibilityState === "visible" ? start() : stop());
  document.addEventListener("visibilitychange", onVisibility);
  start();
  return () => {
    stop();
    document.removeEventListener("visibilitychange", onVisibility);
  };
}

export function useVisiblePolling(refresh: () => void, intervalMs = 3000) {
  const refreshRef = useRef(refresh);
  refreshRef.current = refresh;
  useEffect(() => installVisiblePolling(() => refreshRef.current(), intervalMs), [intervalMs]);
}
