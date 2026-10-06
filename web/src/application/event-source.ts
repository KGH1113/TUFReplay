import type { AppEventMap, AppEvents } from "@/api/app-events";

/** Framework-free subscriptions used by production and mock application services. */
export function createAppEventSource() {
  const listeners = new Map<keyof AppEventMap, Set<(value: never) => void>>();
  const events: AppEvents = {
    on(name, listener) {
      const subscribers = listeners.get(name) ?? new Set();
      subscribers.add(listener as (value: never) => void);
      listeners.set(name, subscribers);
      return () => {
        subscribers.delete(listener as (value: never) => void);
      };
    },
  };
  return {
    events,
    emit<K extends keyof AppEventMap>(name: K, value: AppEventMap[K]) {
      for (const listener of listeners.get(name) ?? []) listener(value as never);
    },
  };
}
