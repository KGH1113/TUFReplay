export interface DomainMessage {
  id: string;
  correlationId?: string;
  payload: unknown;
}

export type Dispose = () => void;
export type SessionState =
  | "connected"
  | "reconnecting"
  | "disconnected"
  | "closed"
  | "incompatible";
export type NamespaceStatus = "initializing" | "ready" | "error" | "unavailable";

/** Application-owned boundary. Transport implementations are injected at composition. */
export interface LocalMessagePeer {
  send(command: string, payload?: object): string;
  on(event: string, listener: (payload: unknown, message: DomainMessage) => void): Dispose;
  onStatus?(
    listener: (status: NamespaceStatus, error?: { code: string; message: string }) => void,
  ): Dispose;
  whenReady(options?: { timeoutMs?: number; signal?: AbortSignal }): Promise<void>;
}

export interface LocalMessageSession {
  state: SessionState;
  onState(listener: (state: SessionState) => void): Dispose;
}

export interface LocalAppChannels {
  namespace: LocalMessagePeer;
  pickerNamespace: LocalMessagePeer;
  rendererNamespace?: LocalMessagePeer;
  session?: LocalMessageSession;
}

export type IpcVersionMismatchDirection = "client_outdated" | "server_outdated" | "legacy_server";
