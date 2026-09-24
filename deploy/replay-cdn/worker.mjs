const MAX_GRANT_SECONDS = 900;
const OBJECT_PATH = /^\/objects\/(?:evidence\/[a-f0-9-]{36}\/[a-zA-Z0-9_-]{1,128}|visual-(?:assets|bundles)\/sha256\/[a-f0-9]{64})$/;

function headers() {
  return new Headers({
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, HEAD, OPTIONS",
    "Access-Control-Allow-Headers": "Range, If-None-Match",
    "Access-Control-Expose-Headers": "Content-Length, Content-Range, ETag, X-Replay-Cache",
    "Cache-Control": "private, no-store",
    "X-Content-Type-Options": "nosniff",
  });
}

function error(status) {
  return new Response(null, { status, headers: headers() });
}

async function authorized(url, secret) {
  if (typeof secret !== "string" || secret.length < 32) return false;
  const expiry = url.searchParams.get("expires");
  const signature = url.searchParams.get("signature");
  const now = Math.floor(Date.now() / 1000);
  if (
    !/^\d{10}$/.test(expiry ?? "") ||
    !/^[a-f0-9]{64}$/.test(signature ?? "") ||
    [...url.searchParams.keys()].length !== 2 ||
    Number(expiry) <= now ||
    Number(expiry) > now + MAX_GRANT_SECONDS + 30
  ) return false;
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw", encoder.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["verify"],
  );
  const bytes = Uint8Array.from(signature.match(/../g), (pair) => Number.parseInt(pair, 16));
  return crypto.subtle.verify("HMAC", key, bytes, encoder.encode(`${expiry}\n${url.pathname}`));
}

/** Grants are validated even on cache hits; unsigned cache keys are never public routes. */
export async function serve(request, env, ctx, cache = caches.default) {
  const url = new URL(request.url);
  if (!OBJECT_PATH.test(url.pathname)) return error(404);
  if (request.method === "OPTIONS") return new Response(null, { status: 204, headers: headers() });
  if (!["GET", "HEAD"].includes(request.method)) return error(405);
  if (!(await authorized(url, env.SIGNING_SECRET))) return error(403);

  const objectKey = url.pathname.slice("/objects/".length);
  const cacheUrl = new URL(url);
  cacheUrl.search = "";
  const cacheHeaders = new Headers();
  for (const name of ["Range", "If-None-Match"]) {
    if (request.headers.has(name)) cacheHeaders.set(name, request.headers.get(name));
  }
  const cacheRequest = new Request(cacheUrl, { headers: cacheHeaders });
  const hit = await cache.match(cacheRequest);
  if (hit) {
    const resultHeaders = new Headers(hit.headers);
    resultHeaders.set("Cache-Control", "private, no-store");
    resultHeaders.set("X-Replay-Cache", "HIT");
    return new Response(request.method === "HEAD" ? null : hit.body, {
      status: hit.status, headers: resultHeaders,
    });
  }

  const object = request.method === "HEAD"
    ? await env.REPLAY_BUCKET.head(objectKey)
    : await env.REPLAY_BUCKET.get(objectKey,
      request.headers.has("Range") ? { range: request.headers } : undefined);
  if (!object) return error(404);
  const resultHeaders = headers();
  resultHeaders.set("Content-Type", objectKey.startsWith("visual-bundles/") ? "application/json" : "application/octet-stream");
  resultHeaders.set("Content-Disposition", "attachment");
  resultHeaders.set("ETag", object.httpEtag);
  resultHeaders.set("Accept-Ranges", "bytes");
  resultHeaders.set("X-Replay-Cache", "MISS");
  let status = 200;
  let length = object.size;
  // R2 may describe the full object as offset=0,length=size. It is only an HTTP
  // partial response when the client actually requested a range.
  if (request.headers.has("Range") && object.range) {
    const offset = object.range.offset ?? Math.max(0, object.size - object.range.suffix);
    length = object.range.length ?? object.size - offset;
    resultHeaders.set("Content-Range", `bytes ${offset}-${offset + length - 1}/${object.size}`);
    status = 206;
  }
  resultHeaders.set("Content-Length", String(length));
  const response = new Response(request.method === "HEAD" ? null : object.body, { status, headers: resultHeaders });
  if (request.method === "GET" && status === 200) {
    const cached = response.clone();
    cached.headers.set("Cache-Control", "public, max-age=31536000, immutable");
    ctx.waitUntil(cache.put(new Request(cacheUrl), cached).catch((error) => {
      console.warn("Replay edge cache write failed", error.name, error.message);
    }));
  }
  return response;
}

export default { fetch: serve };
