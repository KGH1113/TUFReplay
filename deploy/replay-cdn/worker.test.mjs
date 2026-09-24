import { expect, test } from "bun:test";
import { createHmac } from "node:crypto";
import { serve } from "./worker.mjs";

const secret = "s".repeat(32);
const path = `/objects/visual-assets/sha256/${"a".repeat(64)}`;

function grant(target = path, expires = Math.floor(Date.now() / 1000) + 900) {
  const signature = createHmac("sha256", secret).update(`${expires}\n${target}`).digest("hex");
  return `https://cdn.example${target}?expires=${expires}&signature=${signature}`;
}

function harness() {
  const objects = new Map();
  const pending = [];
  let reads = 0;
  const cache = {
    async match(request) { return objects.get(request.url)?.clone(); },
    async put(request, response) { objects.set(request.url, response); },
  };
  const env = { SIGNING_SECRET: secret, REPLAY_BUCKET: {
    async get() {
      reads++;
      return { body: new TextEncoder().encode("payload"), size: 7, httpEtag: '"etag"', range: { offset: 0, length: 7 } };
    },
    async head() { return { size: 7, httpEtag: '"etag"' }; },
  } };
  return { env, cache, ctx: { waitUntil(p) { pending.push(p); } }, pending, reads: () => reads };
}

test("signed GET streams R2 once and validates grants before shared cache hits", async () => {
  const h = harness();
  const get = (url) => serve(new Request(url), h.env, h.ctx, h.cache);
  const first = await get(grant());
  expect(await first.text()).toBe("payload");
  expect(first.headers.get("Access-Control-Allow-Origin")).toBe("*");
  expect(first.headers.get("Cache-Control")).toBe("private, no-store");
  await Promise.all(h.pending);
  const second = await get(grant());
  expect(second.headers.get("X-Replay-Cache")).toBe("HIT");
  expect(await second.text()).toBe("payload");
  expect(h.reads()).toBe(1);
  for (const url of [
    `https://cdn.example${path}`,
    grant().replace("signature=", "signature=0"),
    grant(path, Math.floor(Date.now() / 1000) - 1),
    grant(path, Math.floor(Date.now() / 1000) + 3600),
    grant().replace("/sha256/a", "/sha256/b"),
  ]) expect((await get(url)).status).toBe(403);
  expect(h.reads()).toBe(1);
});

test("only object reads are supported, missing files and ranges remain explicit", async () => {
  const h = harness();
  const request = (url, init) => serve(new Request(url, init), h.env, h.ctx, h.cache);
  expect((await request(grant(), { method: "PUT", body: "replace" })).status).toBe(405);
  expect((await request("https://cdn.example/objects/private/token")).status).toBe(404);
  expect((await request(grant(), { method: "OPTIONS" })).status).toBe(204);
  const head = await request(grant(), { method: "HEAD" });
  expect(head.headers.get("Content-Length")).toBe("7");
  expect(await head.text()).toBe("");
  h.env.REPLAY_BUCKET.get = async (_key, options) => {
    expect(options.range.get("Range")).toBe("bytes=2-4");
    return { body: "ylo", size: 7, httpEtag: '"etag"', range: { offset: 2, length: 3 } };
  };
  const range = await request(grant(), { headers: { Range: "bytes=2-4" } });
  expect(range.status).toBe(206);
  expect(range.headers.get("Content-Range")).toBe("bytes 2-4/7");
  expect(await range.text()).toBe("ylo");
  h.env.REPLAY_BUCKET.get = async () => null;
  expect((await request(grant())).status).toBe(404);
});
