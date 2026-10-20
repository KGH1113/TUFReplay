import type { ReplayApiMod } from "@tuf-replay/ipc";
import type { ZodType } from "zod";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import { ApiError } from "@/shared/errors/api-error";
import { IpcProtocolMismatchError } from "@/shared/errors/ipc-protocol-mismatch-error";

export interface CallOptions {
  timeoutMs?: number;
  signal?: AbortSignal;
}

/** SDK methods own wire validation/correlation; this boundary keeps application error semantics. */
export async function callRecorder<T>(
  clients: LocalAppChannels,
  call: (mod: ReplayApiMod) => Promise<T>,
  options: CallOptions = {},
): Promise<T> {
  try {
    if (options.signal?.aborted) throw options.signal.reason;
    await clients.recorder.whenReady(options);
    if (options.signal?.aborted) throw options.signal.reason;
    return await abortable(call(clients.recorder.mod), options);
  } catch (cause) {
    if (
      cause instanceof ApiError ||
      cause instanceof IpcProtocolMismatchError ||
      options.signal?.aborted
    )
      throw cause;
    const error = cause as { code?: string; message?: string } | null;
    const code =
      error?.code === "TIMEOUT"
        ? "ipc_timeout"
        : error?.code === "UNAVAILABLE"
          ? "ipc_unavailable"
          : (error?.code ?? "ipc_unavailable");
    const protocol = [
      "protocol_mismatch",
      "contract_mismatch",
      "feature_protocol_mismatch",
      "client_outdated",
      "server_outdated",
    ].includes(code);
    const connection = [
      "ipc_unavailable",
      "unavailable",
      "disconnected",
      "disposed",
      "timeout",
      "ipc_timeout",
    ].includes(code);
    const validation =
      ["invalid_response", "invalid_argument"].includes(code) ||
      (cause instanceof Error && cause.name === "IpcValidationError");
    throw new ApiError(error?.message ?? "The local ADOFAI connection failed.", {
      kind: protocol
        ? "protocol"
        : validation
          ? "validation"
          : connection
            ? "connection"
            : "domain",
      code: code === "timeout" ? "ipc_timeout" : code,
      cause,
    });
  }
}

export function decodeRecorder<T>(schema: ZodType<T>, value: unknown): T {
  const result = schema.safeParse(value);
  if (!result.success)
    throw new ApiError("The game sent an invalid response. Reconnect and try again.", {
      kind: "validation",
      code: "invalid_response",
      cause: result.error,
    });
  return result.data;
}

export function abortable<T>(promise: Promise<T>, options: CallOptions = {}): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const finish = (callback: () => void) => {
      clearTimeout(timeout);
      options.signal?.removeEventListener("abort", abort);
      callback();
    };
    const abort = () =>
      finish(() => reject(options.signal?.reason ?? new DOMException("Aborted", "AbortError")));
    const timeout = setTimeout(
      () =>
        finish(() =>
          reject(
            new ApiError("The game did not respond in time. Try again.", {
              kind: "connection",
              code: "ipc_timeout",
            }),
          ),
        ),
      options.timeoutMs ?? 30_000,
    );
    options.signal?.addEventListener("abort", abort, { once: true });
    if (options.signal?.aborted) abort();
    promise.then(
      (value) => finish(() => resolve(value)),
      (error) => finish(() => reject(error)),
    );
  });
}
