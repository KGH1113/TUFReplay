import { Miniflare } from "miniflare";
import { build } from "esbuild";

const bundle = await build({ entryPoints: [new URL("./worker.mjs", import.meta.url).pathname], bundle: true, write: false, format: "esm", platform: "browser" });
const worker = new Miniflare({
  modules: true,
  script: bundle.outputFiles[0].text,
  compatibilityDate: "2026-07-30",
  host: "0.0.0.0",
  port: 8787,
  cachePersist: "/data/cache",
  bindings: {
    S3_ENDPOINT: "http://replay-objects:9000",
    SIGNING_SECRET: "local-game-cdn-signing-secret-not-for-production",
  },
});
await worker.ready;
console.log("Local replay CDN listening on :8787 (production Worker, persistent cache)");
for (const signal of ["SIGTERM", "SIGINT"])
  process.on(signal, async () => { await worker.dispose(); process.exit(0); });
