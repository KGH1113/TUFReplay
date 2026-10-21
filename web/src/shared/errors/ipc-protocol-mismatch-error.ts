import type { IpcVersionMismatchDirection } from "@/ports/local-message-peer";

export class IpcProtocolMismatchError extends Error {
  readonly code = "ipc_version_mismatch";
  constructor(
    readonly direction: IpcVersionMismatchDirection,
    message: string,
  ) {
    super(message);
    this.name = "IpcProtocolMismatchError";
  }
}
