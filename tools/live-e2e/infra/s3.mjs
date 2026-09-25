import S3rver from "s3rver";
import { mkdir } from "node:fs/promises";

await mkdir("/data/objects", { recursive: true });
const server = new S3rver({
  address: "0.0.0.0",
  port: 9000,
  directory: "/data/objects",
  silent: true,
  vhostBuckets: false,
  resetOnClose: false,
  configureBuckets: [{ name: "tuf-replay-local" }],
});
// S3rver otherwise allows anonymous requests. Keep local objects private too.
server.middleware.unshift(async (ctx, next) => {
  ctx.set("X-TUFReplay-Storage", "s3-local");
  if (!ctx.get("authorization").startsWith("AWS4-HMAC-SHA256 ")) {
    ctx.status = 403;
    return;
  }
  await next();
});
await server.run();
console.log("Local private S3 store listening on :9000 (persistent /data/objects)");
for (const signal of ["SIGTERM", "SIGINT"])
  process.on(signal, async () => { await server.close(); process.exit(0); });
