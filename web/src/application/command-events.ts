import type { DomainMessage, LocalMessagePeer } from "@/ports/local-message-peer";

export class DomainCommandError extends Error {
  readonly code: string;
  readonly details?: unknown;
  constructor(code: string, message: string, details?: unknown) {
    super(message);
    this.name = "DomainCommandError";
    this.code = code;
    this.details = details;
  }
}

/** A command completes only when its named domain outcome arrives. */
export function awaitDomainEvent<T>(
  peer: LocalMessagePeer,
  command: string,
  event: string,
  payload: object,
  decode: (value: unknown) => T,
  options: { timeoutMs?: number; signal?: AbortSignal; matches?: (value: T) => boolean } = {},
): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    let commandId: string | undefined;
    let settled = false;
    const early: Array<{ value: unknown; message: DomainMessage; failure: boolean }> = [];
    const subscriptions: Array<() => void> = [];
    const finish = (error?: unknown, value?: T) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      options.signal?.removeEventListener("abort", abort);
      for (const dispose of subscriptions) dispose();
      if (error !== undefined) reject(error);
      else resolve(value as T);
    };
    const receive = (value: unknown, message: DomainMessage, failure: boolean) => {
      if (!commandId) {
        early.push({ value, message, failure });
        return;
      }
      if (message.correlationId !== commandId || settled) return;
      if (failure) {
        const error = value as { code?: unknown; message?: unknown; details?: unknown } | null;
        finish(
          new DomainCommandError(
            typeof error?.code === "string" ? error.code : "command_failed",
            typeof error?.message === "string"
              ? error.message
              : "ADOFAI could not finish this action.",
            error?.details,
          ),
        );
        return;
      }
      try {
        const decoded = decode(value);
        if (!options.matches || options.matches(decoded)) finish(undefined, decoded);
      } catch (error) {
        finish(error);
      }
    };
    const abort = () =>
      finish(options.signal?.reason ?? new DOMException("Cancelled", "AbortError"));
    const timer = setTimeout(
      () =>
        finish(new DomainCommandError("ipc_timeout", "ADOFAI did not finish this action in time.")),
      options.timeoutMs ?? 30_000,
    );
    subscriptions.push(peer.on(event, (value, message) => receive(value, message, false)));
    for (const failure of ["command.rejected", "command.failed"])
      subscriptions.push(peer.on(failure, (value, message) => receive(value, message, true)));
    if (peer.onStatus) {
      const off = peer.onStatus((status, error) => {
        if (status === "unavailable")
          finish(
            new DomainCommandError(
              "ipc_unavailable",
              "The game connection was interrupted. Reconnect and try again.",
            ),
          );
        else if (status === "error")
          finish(
            new DomainCommandError(
              error?.code ?? "namespace_error",
              error?.message ?? "The mod could not initialize.",
            ),
          );
      });
      if (settled) {
        off();
        return;
      }
      subscriptions.push(off);
    }
    options.signal?.addEventListener("abort", abort, { once: true });
    if (options.signal?.aborted) {
      abort();
      return;
    }
    try {
      commandId = peer.send(command, payload);
      for (const queued of early) receive(queued.value, queued.message, queued.failure);
    } catch (error) {
      finish(error);
    }
  });
}
