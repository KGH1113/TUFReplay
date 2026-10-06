import type { AppEventMap, AppEvents } from "@/api/app-events";
import { ApiError } from "@/shared/errors/api-error";

/** Waits for authoritative push state, including an already captured snapshot. */
export function waitForAppState<K extends keyof AppEventMap>(
  events: AppEvents,
  name: K,
  finished: (state: AppEventMap[K]) => boolean,
  options: {
    namespace?: "recorder" | "renderer";
    signal?: AbortSignal;
    timeoutMs?: number;
    onState?: (state: AppEventMap[K]) => void;
  } = {},
): Promise<AppEventMap[K]> {
  return new Promise((resolve, reject) => {
    let settled = false;
    const cleanup: Array<() => void> = [];
    let timer: ReturnType<typeof setTimeout> | undefined;
    const finish = (error?: unknown, state?: AppEventMap[K]) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      options.signal?.removeEventListener("abort", abort);
      for (const off of cleanup) off();
      if (error !== undefined) reject(error);
      else resolve(state as AppEventMap[K]);
    };
    const abort = () =>
      finish(options.signal?.reason ?? new DOMException("Cancelled", "AbortError"));
    const subscribe = (off: () => void) => {
      if (settled) off();
      else cleanup.push(off);
    };
    subscribe(
      events.on(name, (state) => {
        try {
          options.onState?.(state);
          if (finished(state)) finish(undefined, state);
        } catch (error) {
          finish(error);
        }
      }),
    );
    if (!settled)
      subscribe(
        events.on("connection.changed", (state) => {
          if (
            state === "reconnecting" ||
            state === "disconnected" ||
            state === "closed" ||
            state === "incompatible"
          )
            finish(
              new ApiError(
                "The game connection was interrupted. Reconnect before starting again.",
                { kind: "connection", code: "ipc_unavailable" },
              ),
            );
        }),
      );
    if (!settled && options.namespace)
      subscribe(
        events.on(`${options.namespace}.status.changed`, (status) => {
          if (status === "unavailable" || status === "error")
            finish(
              new ApiError("The mod is unavailable. Enable it and start this action again.", {
                kind: "connection",
                code: "ipc_unavailable",
              }),
            );
        }),
      );
    if (settled) return;
    options.signal?.addEventListener("abort", abort, { once: true });
    if (options.signal?.aborted) {
      abort();
      return;
    }
    if (options.timeoutMs)
      timer = setTimeout(
        () =>
          finish(
            new ApiError("The game did not finish this action in time.", {
              kind: "connection",
              code: "ipc_timeout",
            }),
          ),
        options.timeoutMs,
      );
  });
}
