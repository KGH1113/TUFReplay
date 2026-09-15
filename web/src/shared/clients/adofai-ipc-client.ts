import {
  AdofaiIpcClient,
  type AdofaiIpcNamespaceClient,
  type IpcVersionMismatchError,
  tryConnect,
} from "@adofai-ipc/client";
import type { ZodType } from "zod";
import { adofaiIpcFetch } from "@/shared/clients/adofai-ipc-fetch";
import { ApiError } from "@/shared/errors/api-error";

const NAMESPACE = "tuf-replay";
const IPC_PROBE_TIMEOUT_MS = 500;
const IPC_REQUEST_TIMEOUT_MS = 30_000;
const NAMESPACE_READY_TIMEOUT_MS = 30_000;

interface DomainErrorPayload {
  error: {
    code: string;
    message: string;
  };
}

export interface AdofaiIpcClients {
  namespace: AdofaiIpcNamespaceClient;
  pickerNamespace: AdofaiIpcNamespaceClient;
}

let clientsPromise: Promise<AdofaiIpcClients> | null = null;

export function getAdofaiIpcClients(
  onVersionMismatch?: (error: IpcVersionMismatchError) => void,
): Promise<AdofaiIpcClients> {
  clientsPromise ??= connect(onVersionMismatch).catch((cause) => {
    clientsPromise = null;
    throw cause;
  });
  return clientsPromise;
}

export function resetAdofaiIpcClients() {
  clientsPromise = null;
}

export async function callAdofaiIpc<TResult>(
  namespace: Pick<AdofaiIpcNamespaceClient, "call">,
  method: string,
  params: object,
  schema: ZodType<TResult>,
): Promise<TResult> {
  let result: unknown;
  try {
    result = await namespace.call(method, params);
  } catch (cause) {
    throw new ApiError("The local TUFReplay connection failed.", {
      kind: "connection",
      code: connectionErrorCode(cause),
      cause,
    });
  }

  if (isDomainError(result)) {
    throw new ApiError(result.error.message, {
      kind: "domain",
      code: result.error.code,
    });
  }

  const parsed = schema.safeParse(result);
  if (!parsed.success) {
    throw new ApiError(`Invalid AdofaiIpc response: ${method}`, {
      kind: "validation",
      code: "invalid_response",
      cause: parsed.error,
    });
  }
  return parsed.data;
}

function connectionErrorCode(cause: unknown) {
  const code = errorString(cause, "code");
  if (code) {
    if (code === "UNAVAILABLE") return "ipc_unavailable";
    if (code === "TIMEOUT") return "ipc_timeout";
    if (code === "VERSION_MISMATCH") return "ipc_version_mismatch";
    return code;
  }

  const name = errorString(cause, "name").toLowerCase();
  const message = errorString(cause, "message").toLowerCase();
  if (name.includes("timeout") || message.includes("timeout") || message.includes("timed out"))
    return "ipc_timeout";
  if (cause instanceof TypeError || message.includes("fetch") || message.includes("connection"))
    return "ipc_unavailable";
  return "ipc_request_failed";
}

function errorString(cause: unknown, key: "code" | "name" | "message") {
  if (!cause || typeof cause !== "object" || !(key in cause)) return "";
  const value = (cause as Record<string, unknown>)[key];
  return typeof value === "string" ? value : "";
}

async function connect(
  onVersionMismatch?: (error: IpcVersionMismatchError) => void,
): Promise<AdofaiIpcClients> {
  const client = await tryConnect({
    fetch: adofaiIpcFetch,
    probeTimeoutMs: IPC_PROBE_TIMEOUT_MS,
    requestTimeoutMs: IPC_REQUEST_TIMEOUT_MS,
    onVersionMismatch,
  });
  await client.waitForNamespace(NAMESPACE, {
    status: "ready",
    timeoutMs: NAMESPACE_READY_TIMEOUT_MS,
  });
  const pickerClient = new AdofaiIpcClient({
    baseUrl: client.baseUrl,
    fetch: adofaiIpcFetch,
    requestTimeoutMs: IPC_REQUEST_TIMEOUT_MS,
  });
  return {
    namespace: client.namespace(NAMESPACE),
    pickerNamespace: pickerClient.namespace(NAMESPACE),
  };
}

function isDomainError(value: unknown): value is DomainErrorPayload {
  if (!value || typeof value !== "object") return false;
  const error = (value as { error?: unknown }).error;
  return Boolean(
    error &&
      typeof error === "object" &&
      typeof (error as { code?: unknown }).code === "string" &&
      typeof (error as { message?: unknown }).message === "string",
  );
}
