export type IpcConnectionState = "connected" | "reconnecting" | "disconnected" | "closed" | "incompatible";
export type IpcNamespaceStatus = "initializing" | "ready" | "error" | "unavailable";
export interface IpcErrorInfo { code: string; message: string }
export interface IpcMessage<TPayload = unknown> {
  kind: string;
  namespace?: string | null;
  name: string;
  id: string;
  correlationId?: string;
  payload: TPayload;
}
/** Byte transport only. Protocol, subscription and reconnection belong to the SDK. */
export interface IpcTransport {
  connect(): Promise<void>;
  send(text: string): void | Promise<void>;
  close(): void | Promise<void>;
  onMessage(listener: (text: string) => void): () => void;
  onClose(listener: (reason?: unknown) => void): () => void;
}
export type IpcTransportFactory = (url: string, protocol: string) => IpcTransport;
export interface ConnectIpcOptions {
  host?: "127.0.0.1" | "localhost";
  startPort?: number;
  endPort?: number;
  connectTimeoutMs?: number;
  transportFactory?: IpcTransportFactory;
  onProtocolMismatch?: (error: import("./errors").IpcProtocolMismatchError) => void;
}
export interface IpcCommandOptions { id?: string; signal?: AbortSignal }
export interface IpcReadyOptions { timeoutMs?: number; signal?: AbortSignal }
