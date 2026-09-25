import { serve } from "../../../deploy/replay-cdn/worker.mjs";
import { s3Bucket } from "./s3-bucket.mjs";

export default {
  async fetch(request, env, ctx) {
    const bucket = s3Bucket(env.S3_ENDPOINT, "tuf-replay-local");
    if (new URL(request.url).pathname === "/__health") {
      // A signed S3 request verifies connectivity, not merely a listening port.
      await bucket.head("health-probe");
      return Response.json({ service: "tuf-replay-local-cdn", storage: "s3-local" });
    }
    return serve(request, { SIGNING_SECRET: env.SIGNING_SECRET, REPLAY_BUCKET: bucket }, ctx);
  },
};
