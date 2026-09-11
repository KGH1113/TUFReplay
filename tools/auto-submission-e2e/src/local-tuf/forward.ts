import { outgoingToken } from "../config";
import { log } from "../logs";
import { localTufBase } from "./config";
import { loseRegistrationResponse } from "../mock/receipts";

export async function forwardToLocalTuf(request: Request, body: any): Promise<Response> {
  const url = new URL(request.url);
  const destination = new URL(`${url.pathname}${url.search}`, localTufBase);
  const response = await fetch(destination, {
    method: request.method,
    redirect: "error",
    headers: { Authorization: `Bearer ${outgoingToken}`, "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(15000),
  });
  const result = await response.json();
  log("tuf", `LOCAL TUF ${response.status} ${destination.pathname}`, result, body?.run_id);
  if (response.ok && url.pathname.endsWith("/register") && loseRegistrationResponse(body.run_id)) {
    log("tuf", "로컬 TUF 등록 후 응답 유실 주입", result, body.run_id);
    return new Response(new ReadableStream({
      start(controller) { controller.error(new Error("injected lost response")); },
    }));
  }
  return Response.json(result, { status: response.status });
}
