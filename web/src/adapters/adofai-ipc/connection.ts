import { DomainCommandError } from "@/application/command-events";
import type { DomainMessage, LocalAppChannels, LocalMessagePeer } from "@/ports/local-message-peer";
import { IpcProtocolMismatchError } from "@/shared/errors/ipc-protocol-mismatch-error";
import {
  type IpcChannel,
  IpcConnection,
  type IpcMessage,
  PROTOCOL_VERSION,
  IpcProtocolMismatchError as TransportProtocolMismatchError,
} from "../../../vendor/adofai-ipc/src";

const stateEvents = [
  "downloads.state.changed",
  "render-bundle.state.changed",
  "renderer.job.changed",
  "renderer.folder-selection.changed",
  "renderer.health.snapshot",
  "health.snapshot",
  "activity.changed",
  "replay.state.changed",
  "calibration.state.changed",
  "microphone.devices.changed",
  "webcam.state.changed",
  "renderer.state.changed",
  "renderer.settings.changed",
];
let channelsPromise: Promise<LocalAppChannels> | undefined;

export function getAdofaiIpcClients(): Promise<LocalAppChannels> {
  channelsPromise ??= connect().catch((error: unknown) => {
    channelsPromise = undefined;
    throw translateError(error);
  });
  return channelsPromise;
}

async function connect(): Promise<LocalAppChannels> {
  const connection = new IpcConnection({ connectTimeoutMs: 500 });
  // Capture subscriptions and state before discovery can deliver the first snapshot.
  const recorder = adaptChannel(connection.namespace("tuf-replay"));
  const renderer = adaptChannel(connection.namespace("tuf-replay-renderer"));
  try {
    await connection.start();
  } catch (error) {
    connection.close();
    throw error;
  }
  return {
    namespace: recorder,
    pickerNamespace: recorder,
    rendererNamespace: renderer,
    session: {
      get state() {
        return connection.state;
      },
      onState: (listener) => connection.onState(listener),
    },
  };
}

export function adaptChannel(channel: IpcChannel): LocalMessagePeer {
  const snapshots = new Map<string, { name: string; payload: unknown; message: DomainMessage }>();
  for (const name of stateEvents)
    channel.on(name, (payload, message) => {
      const state = payload as { jobId?: string; selectionId?: string } | null;
      const identity = state?.jobId ?? state?.selectionId ?? "";
      const key = `${name}:${identity}`;
      snapshots.delete(key);
      snapshots.set(key, { name, payload, message });
      if (snapshots.size > 128) snapshots.delete(snapshots.keys().next().value as string);
    });
  channel.onStatus((status) => {
    if (status === "unavailable") snapshots.clear();
  });
  return {
    send(command, payload) {
      try {
        return channel.send(command, payload);
      } catch (error) {
        throw translateError(error);
      }
    },
    on(name, listener) {
      const off = channel.on(name, (payload, message: IpcMessage) => listener(payload, message));
      for (const snapshot of snapshots.values())
        if (snapshot.name === name) listener(snapshot.payload, snapshot.message);
      return off;
    },
    onStatus: (listener) => channel.onStatus(listener),
    async whenReady(options) {
      try {
        await channel.whenReady(options);
      } catch (error) {
        throw translateError(error);
      }
    },
  };
}

function translateError(error: unknown): unknown {
  if (error instanceof TransportProtocolMismatchError) {
    const direction =
      error.protocolVersion === null
        ? "legacy_server"
        : error.protocolVersion > PROTOCOL_VERSION
          ? "client_outdated"
          : "server_outdated";
    return new IpcProtocolMismatchError(direction, error.message);
  }
  if (error && typeof error === "object" && "code" in error) {
    const code = String(error.code);
    if (code === "UNAVAILABLE" || code === "TIMEOUT")
      return new DomainCommandError(
        code === "TIMEOUT" ? "ipc_timeout" : "ipc_unavailable",
        error instanceof Error ? error.message : "The game connection failed.",
      );
  }
  return error;
}

/** Keeps the injected application ports stable while first connection attempts can be retried. */
export function createLazyAppChannels(
  load: () => Promise<LocalAppChannels> = getAdofaiIpcClients,
): LocalAppChannels {
  let current: LocalAppChannels | undefined;
  let pending: Promise<LocalAppChannels> | undefined;
  let state: import("@/ports/local-message-peer").SessionState = "disconnected";
  const states = new Set<(value: typeof state) => void>();
  const waiting = new Set<() => void>();
  const setState = (next: typeof state) => {
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
  const proxy = (name: "namespace" | "rendererNamespace"): LocalMessagePeer => ({
    send(command, payload) {
      const peer = current?.[name];
      if (!peer)
        throw new DomainCommandError(
          "ipc_unavailable",
          "Connect to the game before trying this action.",
        );
      return peer.send(command, payload);
    },
    on(event, listener) {
      let off = () => {};
      let disposed = false;
      const bind = () => {
        if (!disposed) off = current?.[name]?.on(event, listener) ?? (() => {});
      };
      if (current) bind();
      else waiting.add(bind);
      return () => {
        disposed = true;
        waiting.delete(bind);
        off();
      };
    },
    onStatus(listener) {
      let off = () => {};
      let disposed = false;
      const bind = () => {
        if (!disposed) off = current?.[name]?.onStatus?.(listener) ?? (() => {});
      };
      if (current) bind();
      else waiting.add(bind);
      return () => {
        disposed = true;
        waiting.delete(bind);
        off();
      };
    },
    async whenReady(options) {
      const peer = (await get())[name];
      if (!peer)
        throw new DomainCommandError(
          "renderer_missing",
          "Install TUFReplay-Renderer and restart the game.",
        );
      await peer.whenReady(options);
    },
  });
  const recorder = proxy("namespace");
  return {
    namespace: recorder,
    pickerNamespace: recorder,
    rendererNamespace: proxy("rendererNamespace"),
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
