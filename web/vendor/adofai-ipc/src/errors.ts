export class IpcConnectionError extends Error {
  readonly code: string = "UNAVAILABLE";
  constructor(message = "ADOFAI-IPC is unavailable. Start the game with ADOFAI-IPC v1.0.0 enabled.") {
    super(message); this.name = "IpcConnectionError";
  }
}
export class IpcUnavailableError extends IpcConnectionError {
  constructor(message?: string) { super(message); this.name = "IpcUnavailableError"; }
}
export class IpcTimeoutError extends IpcConnectionError {
  override readonly code = "TIMEOUT";
  constructor(readonly timeoutMs: number) { super(`ADOFAI-IPC did not become ready within ${timeoutMs} ms.`); this.name = "IpcTimeoutError"; }
}
export class IpcProtocolMismatchError extends Error {
  readonly code = "INCOMPATIBLE";
  readonly clientVersion = "1.0.0";
  constructor(readonly protocolVersion: number | null, readonly serverVersion: string | null) {
    super("The game and application use different ADOFAI-IPC protocols. Update them and restart the game.");
    this.name = "IpcProtocolMismatchError";
  }
}
export class IpcNamespaceError extends Error {
  constructor(readonly code: string, message: string) { super(message); this.name = "IpcNamespaceError"; }
}
export function isIpcUnavailable(error: unknown): error is IpcConnectionError {
  return error instanceof IpcConnectionError && error.code === "UNAVAILABLE";
}
