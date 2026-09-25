import { timingSafeEqual } from "node:crypto";
import { InputError, parseText, parseUserId, type TesterRepository } from "./testers.model";
import { lookupLinkedPlayer, parsePlayerId, PlayerLookupError, type LinkedPlayer } from "./player-lookup";

export interface AdminAssets {
  html: string;
  css: string;
  javascript: string;
}

export interface AdminCredentials {
  username: string;
  password: string;
}

const securityHeaders = {
  "Cache-Control": "no-store",
  "Content-Security-Policy":
    "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
  "Referrer-Policy": "no-referrer",
  "X-Content-Type-Options": "nosniff",
  "X-Frame-Options": "DENY",
};

function sameSecret(left: string, right: string): boolean {
  const a = Buffer.from(left);
  const b = Buffer.from(right);
  return a.length === b.length && timingSafeEqual(a, b);
}

function authorized(request: Request, credentials: AdminCredentials): boolean {
  const header = request.headers.get("authorization");
  if (!header?.startsWith("Basic ")) return false;
  const decoded = Buffer.from(header.slice(6), "base64").toString("utf8");
  const colon = decoded.indexOf(":");
  if (colon < 0) return false;
  return (
    sameSecret(decoded.slice(0, colon), credentials.username) &&
    sameSecret(decoded.slice(colon + 1), credentials.password)
  );
}

function json(body: unknown, status = 200, extraHeaders: Record<string, string> = {}): Response {
  return Response.json(body, { status, headers: { ...securityHeaders, ...extraHeaders } });
}

async function input(request: Request): Promise<Record<string, unknown>> {
  if (!request.headers.get("content-type")?.startsWith("application/json")) {
    throw new InputError("JSON 요청이 필요합니다.");
  }
  try {
    const body: unknown = await request.json();
    if (!body || typeof body !== "object" || Array.isArray(body)) throw new Error();
    return body as Record<string, unknown>;
  } catch {
    throw new InputError("요청 내용을 확인해 주세요.");
  }
}

export function createAdminHandler(
  repository: TesterRepository,
  credentials: AdminCredentials,
  assets: AdminAssets,
  resolvePlayer: (playerId: number) => Promise<LinkedPlayer> = lookupLinkedPlayer,
): (request: Request) => Promise<Response> {
  return async (request) => {
    const url = new URL(request.url);
    if (url.pathname === "/healthz" && request.method === "GET") {
      return json({ ok: true });
    }
    if (!authorized(request, credentials)) {
      return json({ error: "관리자 인증이 필요합니다." }, 401, {
        "WWW-Authenticate": 'Basic realm="TUFReplay trusted testers", charset="UTF-8"',
      });
    }
    if (request.method === "POST" && request.headers.get("origin") !== url.origin) {
      return json({ error: "이 페이지에서 다시 시도해 주세요." }, 403);
    }
    try {
      if (request.method === "GET" && url.pathname === "/") {
        return new Response(assets.html, {
          headers: { ...securityHeaders, "Content-Type": "text/html; charset=utf-8" },
        });
      }
      if (request.method === "GET" && url.pathname === "/app.css") {
        return new Response(assets.css, {
          headers: { ...securityHeaders, "Content-Type": "text/css; charset=utf-8" },
        });
      }
      if (request.method === "GET" && url.pathname === "/app.js") {
        return new Response(assets.javascript, {
          headers: { ...securityHeaders, "Content-Type": "text/javascript; charset=utf-8" },
        });
      }
      if (request.method === "GET" && url.pathname === "/api/testers") {
        return json(await repository.list());
      }
      const playerLookup = /^\/api\/players\/([^/]+)$/.exec(url.pathname);
      if (request.method === "GET" && playerLookup) {
        return json(await resolvePlayer(parsePlayerId(playerLookup[1])));
      }
      if (request.method === "POST" && url.pathname === "/api/testers/by-player") {
        const body = await input(request);
        const playerId = parsePlayerId(body.playerId);
        const expectedUserId = parseUserId(body.expectedUserId);
        const label = parseText(body.label, "표시 이름", 80, false);
        const reason = parseText(body.reason, "승인 사유", 240, true);
        const player = await resolvePlayer(playerId);
        if (player.userId !== expectedUserId) {
          return json({ error: "연결된 계정이 변경됐습니다. 플레이어를 다시 조회해 주세요." }, 409);
        }
        return json(await repository.grant(player.userId, label || player.username, credentials.username, reason), 201);
      }
      if (request.method === "POST" && url.pathname === "/api/testers") {
        const body = await input(request);
        const userId = parseUserId(body.userId);
        const label = parseText(body.label, "표시 이름", 80, false);
        const reason = parseText(body.reason, "승인 사유", 240, true);
        return json(await repository.grant(userId, label, credentials.username, reason), 201);
      }
      const revoke = /^\/api\/testers\/([^/]+)\/revoke$/.exec(url.pathname);
      if (request.method === "POST" && revoke) {
        const body = await input(request);
        const userId = parseUserId(revoke[1]);
        const reason = parseText(body.reason, "비활성화 사유", 240, true);
        const updated = await repository.revoke(userId, credentials.username, reason);
        if (!updated) return json({ error: "활성 테스터를 찾지 못했습니다. 목록을 새로고침해 주세요." }, 404);
        return json(updated);
      }
      return json({ error: "페이지를 찾지 못했습니다." }, 404);
    } catch (error) {
      if (error instanceof InputError) return json({ error: error.message }, 400);
      if (error instanceof PlayerLookupError) return json({ error: error.message }, error.status);
      // Do not send SQL text, database URLs, or stack traces to the browser.
      return json({ error: "변경을 저장하지 못했습니다. 잠시 후 다시 시도해 주세요." }, 500);
    }
  };
}
