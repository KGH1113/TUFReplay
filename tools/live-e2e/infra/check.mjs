import { strict as assert } from "node:assert";
import { createHash, createHmac, randomUUID } from "node:crypto";
import { AwsClient } from "aws4fetch";

const aws = new AwsClient({ accessKeyId: "S3RVER", secretAccessKey: "S3RVER", service: "s3", region: "auto" });
const bytes = Buffer.from(`local delivery check ${randomUUID()}`);
const key = `evidence/${randomUUID()}/${createHash("sha256").update(bytes).digest("hex")}`;
const source = `http://replay-objects:9000/tuf-replay-local/${key}`;
const pathname = `/objects/${key}`;
const expires = Math.floor(Date.now() / 1000) + 300;
function grant(expiry = expires) {
  const signature = createHmac("sha256", "local-game-cdn-signing-secret-not-for-production").update(`${expiry}\n${pathname}`).digest("hex");
  return `http://127.0.0.1:8787${pathname}?expires=${expiry}&signature=${signature}`;
}
try {
  assert.equal((await aws.fetch(source, { method: "PUT", body: bytes })).status, 200);
  assert.equal((await fetch(`http://127.0.0.1:8787${pathname}`)).status, 403);
  const first = await fetch(grant());
  assert.equal(first.status, 200);
  assert.equal(first.headers.get("Access-Control-Allow-Origin"), "*");
  assert.deepEqual(Buffer.from(await first.arrayBuffer()), bytes);
  let hit;
  for (let attempt = 0; attempt < 20; attempt++) {
    hit = await fetch(grant());
    await hit.arrayBuffer();
    if (hit.headers.get("X-Replay-Cache") === "HIT") break;
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  assert.equal(hit.headers.get("X-Replay-Cache"), "HIT");
  assert.equal((await fetch(grant(expires - 1000))).status, 403);
  const range = await fetch(grant(), { headers: { Range: "bytes=1-4" } });
  assert.equal(range.status, 206);
  assert.deepEqual(Buffer.from(await range.arrayBuffer()), bytes.subarray(1, 5));
  const head = await fetch(grant(), { method: "HEAD" });
  assert.equal(head.status, 200);
  assert.equal(Number(head.headers.get("Content-Length")), bytes.length);
  console.log("PASS: S3 upload → production CDN Worker → exact bytes, CORS, Range, HEAD, cache HIT, expired/unsigned rejection");
} finally {
  await aws.fetch(source, { method: "DELETE" });
}
