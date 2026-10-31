import { type ReplayApiAppHandlers, ReplayApiContract, type ReplayApiMod } from "@tuf-replay/ipc";
import { createIpc } from "adofai-ipc";
import { PROTOCOL_VERSION } from "adofai-ipc/transport";
import { observeBrowserConnection } from "@/adapters/adofai-ipc/browser-connection-lifecycle";
import { abortable } from "@/api/recorder-call";
import type {
  LocalAppChannels,
  RecorderClient,
  RecorderEvent,
  RecorderEventName,
  SessionState,
} from "@/ports/local-message-peer";
import type { OrbitRenderClient } from "@/ports/orbit-render-client";
import { IpcProtocolMismatchError } from "@/shared/errors/ipc-protocol-mismatch-error";
import { OrbitRenderIpcContract } from "../../../vendor/orbitrender-ipc/index";

export function createRecorderSnapshots() {
  const latest = new Map<string, { name: RecorderEventName; value: unknown }>();
  const listeners = new Map<RecorderEventName, Set<(value: never) => void>>();
  const publish = <K extends RecorderEventName>(name: K, value: RecorderEvent<K>) => {
    const identity =
      name === "renderBundleStateChanged"
        ? (value as RecorderEvent<"renderBundleStateChanged">).jobId
        : "";
    const key = `${name}:${identity}`;
    latest.delete(key);
    latest.set(key, { name, value });
    if (latest.size > 128) latest.delete(latest.keys().next().value as string);
    for (const listener of listeners.get(name) ?? []) listener(value as never);
  };
  const handlers: ReplayApiAppHandlers = {
    healthSnapshot: ({ value }) => publish("healthSnapshot", value),
    activityChanged: ({ value }) => publish("activityChanged", value),
    replayStateChanged: ({ value }) => publish("replayStateChanged", value),
    microphoneDevicesChanged: ({ value }) => publish("microphoneDevicesChanged", value),
    microphoneTimingChanged: ({ value }) => publish("microphoneTimingChanged", value),
    calibrationStateChanged: ({ value }) => publish("calibrationStateChanged", value),
    webcamStateChanged: ({ value }) => publish("webcamStateChanged", value),
    ffmpegStateChanged: ({ value }) => publish("ffmpegStateChanged", value),
    downloadsStateChanged: ({ value }) => publish("downloadsStateChanged", value),
    renderBundleStateChanged: ({ value }) => publish("renderBundleStateChanged", value),
  };
  const on: RecorderClient["on"] = (name, listener) => {
    const set = listeners.get(name) ?? new Set();
    set.add(listener as (value: never) => void);
    listeners.set(name, set);
    for (const entry of latest.values()) if (entry.name === name) listener(entry.value as never);
    return () => {
      set.delete(listener as (value: never) => void);
    };
  };
  return { handlers, on, clear: () => latest.clear() };
}

export async function getAdofaiIpcClients(): Promise<LocalAppChannels> {
  const snapshots = createRecorderSnapshots();
  let mismatch: IpcProtocolMismatchError | undefined;
  const app = createIpc({
    requestTimeoutMs: 180_000,
    onProtocolMismatch(error) {
      const direction =
        error.protocolVersion === null
          ? "legacy_server"
          : error.protocolVersion > PROTOCOL_VERSION
            ? "client_outdated"
            : "server_outdated";
      mismatch = new IpcProtocolMismatchError(direction, error.message);
    },
  }).registerApp({ displayName: "TUFReplay Companion" });
  // The generated handlers exist before use() starts discovery and snapshot delivery.
  const channel = app.use(ReplayApiContract, snapshots.handlers);
  const orbit = app.use(OrbitRenderIpcContract, {});
  let state: SessionState = "reconnecting";
  let closed = false;
  const states = new Set<(value: SessionState) => void>();
  channel.onStatus((status, error) => {
    if (status !== "ready") snapshots.clear();
    state = closed
      ? "closed"
      : status === "ready"
        ? "connected"
        : status === "initializing"
          ? "reconnecting"
          : error?.code === "contract_mismatch" ||
              error?.code === "protocol_mismatch" ||
              error?.code === "feature_protocol_mismatch"
            ? "incompatible"
            : "disconnected";
    for (const listener of states) listener(state);
  });
  if (typeof window !== "undefined" && typeof document !== "undefined") {
    observeBrowserConnection({
      checkConnection: () => app.checkConnection(),
      dispose() {
        closed = true;
        app.dispose();
      },
    });
  }
  return {
    orbit,
    recorder: {
      mod: channel,
      async whenReady(options) {
        try {
          await abortable(channel.ready(), options);
        } catch (error) {
          throw mismatch ?? error;
        }
      },
      on: snapshots.on,
      onStatus: (listener) => channel.onStatus(listener),
    },
    session: {
      get state() {
        return state;
      },
      onState(listener) {
        states.add(listener);
        listener(state);
        return () => {
          states.delete(listener);
        };
      },
    },
  };
}

/** Keeps application ports stable across a failed initial discovery attempt. */
export function createLazyAppChannels(
  load: () => Promise<LocalAppChannels> = getAdofaiIpcClients,
): LocalAppChannels {
  let current: LocalAppChannels | undefined;
  let pending: Promise<LocalAppChannels> | undefined;
  let state: SessionState = "disconnected";
  const states = new Set<(value: SessionState) => void>();
  const waiting = new Set<() => void>();
  const setState = (next: SessionState) => {
    state = next;
    for (const listener of states) listener(next);
  };
  const get = () => {
    pending ??= (async () => {
      setState("reconnecting");
      try {
        current = await load();
        current.session?.onState(setState);
        for (const bind of waiting) bind();
        waiting.clear();
        return current;
      } catch (error) {
        pending = undefined;
        setState(error instanceof IpcProtocolMismatchError ? "incompatible" : "disconnected");
        throw error;
      }
    })();
    return pending;
  };
  const bindLater = (subscribe: (clients: LocalAppChannels) => () => void) => {
    let disposed = false;
    let off = () => {};
    const bind = () => {
      if (!disposed && current) off = subscribe(current);
    };
    if (current) bind();
    else waiting.add(bind);
    return () => {
      disposed = true;
      waiting.delete(bind);
      off();
    };
  };
  // This proxy preserves the generated method signatures while delaying app registration.
  // It never maps methods to transport commands; the generated channel owns that contract.
  const mod = new Proxy({} as ReplayApiMod, {
    get(_target, name) {
      if (name === "then") return undefined;
      return async (...args: unknown[]) => {
        const clients = await get();
        const method = clients.recorder.mod[name as keyof ReplayApiMod] as (
          ...args: unknown[]
        ) => unknown;
        return method(...args);
      };
    },
  });
  const orbit = new Proxy({} as OrbitRenderClient, {
    get(_target, name) {
      if (name === "then") return undefined;
      return async (...args: unknown[]) => {
        const clients = await get();
        if (!clients.orbit) throw new Error("OrbitRender is not available.");
        const method = clients.orbit[name as keyof OrbitRenderClient] as (
          ...args: unknown[]
        ) => unknown;
        return method(...args);
      };
    },
  });
  return {
    orbit,
    recorder: {
      mod,
      async whenReady(options) {
        const clients = await abortable(get(), options);
        await clients.recorder.whenReady(options);
      },
      on: (name, listener) => bindLater((clients) => clients.recorder.on(name, listener)),
      onStatus: (listener) => bindLater((clients) => clients.recorder.onStatus(listener)),
    },
    session: {
      get state() {
        return state;
      },
      onState(listener) {
        states.add(listener);
        listener(state);
        return () => {
          states.delete(listener);
        };
      },
    },
  };
}
