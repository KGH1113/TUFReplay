interface LegacyIpcError {
  Code?: unknown;
  Message?: unknown;
  code?: unknown;
  message?: unknown;
}

interface LegacyIpcResponse {
  Ok?: unknown;
  Result?: unknown;
  Error?: LegacyIpcError | null;
  Id?: unknown;
}

export const adofaiIpcFetch = (async (...args: Parameters<typeof fetch>) => {
  const response = await globalThis.fetch(...args);
  const input = args[0];
  const requestUrl = String(input instanceof Request ? input.url : input);
  if (!requestUrl.endsWith("/ipc")) return response;

  const text = await response.clone().text();
  if (!text) return response;

  try {
    const payload: unknown = JSON.parse(text);
    if (!isLegacyIpcResponse(payload)) return response;

    return new Response(
      JSON.stringify({
        ok: payload.Ok,
        result: payload.Result,
        error: normalizeError(payload.Error),
        id: payload.Id,
      }),
      {
        status: response.status,
        statusText: response.statusText,
        headers: response.headers,
      },
    );
  } catch {
    return response;
  }
}) as typeof fetch;

function isLegacyIpcResponse(value: unknown): value is LegacyIpcResponse {
  if (!value || typeof value !== "object") return false;
  return "Ok" in value || "Result" in value || "Error" in value || "Id" in value;
}

function normalizeError(error: LegacyIpcError | null | undefined) {
  if (!error) return error;
  return {
    code: error.code ?? error.Code,
    message: error.message ?? error.Message,
  };
}
