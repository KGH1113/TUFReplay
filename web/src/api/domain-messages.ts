import type { ZodType } from "zod";
import { awaitDomainEvent, DomainCommandError } from "@/application/command-events";
import type { LocalMessagePeer } from "@/ports/local-message-peer";
import { ApiError } from "@/shared/errors/api-error";
import { IpcProtocolMismatchError } from "@/shared/errors/ipc-protocol-mismatch-error";

export type { LocalAppChannels as AdofaiIpcClients } from "@/ports/local-message-peer";

export async function sendDomainCommand<TResult>(
  peer: LocalMessagePeer,
  command: string,
  event: string,
  payload: object,
  schema: ZodType<TResult>,
  options?: { timeoutMs?: number; signal?: AbortSignal; matches?: (value: TResult) => boolean },
): Promise<TResult> {
  try {
    await peer.whenReady({ timeoutMs: options?.timeoutMs ?? 30_000, signal: options?.signal });
    return await awaitDomainEvent(
      peer,
      command,
      event,
      payload,
      (value) => {
        const parsed = schema.safeParse(value);
        if (!parsed.success)
          throw new ApiError(`Invalid ADOFAI message: ${event}`, {
            kind: "validation",
            code: "invalid_response",
            cause: parsed.error,
          });
        return parsed.data;
      },
      options,
    );
  } catch (cause) {
    if (cause instanceof ApiError || cause instanceof IpcProtocolMismatchError) throw cause;
    const code = cause instanceof DomainCommandError ? cause.code : "ipc_unavailable";
    throw new ApiError(
      cause instanceof Error ? cause.message : "The local ADOFAI connection failed.",
      {
        kind: code === "ipc_unavailable" || code === "ipc_timeout" ? "connection" : "domain",
        code,
        cause,
      },
    );
  }
}

export function observeDomainEvent<TResult>(
  peer: LocalMessagePeer,
  event: string,
  schema: ZodType<TResult>,
  listener: (value: TResult) => void,
  onError?: (error: unknown) => void,
) {
  return peer.on(event, (payload) => {
    const result = schema.safeParse(payload);
    if (result.success) listener(result.data);
    else
      onError?.(
        new ApiError(`Invalid ADOFAI message: ${event}`, {
          kind: "validation",
          code: "invalid_response",
          cause: result.error,
        }),
      );
  });
}
