import assert from "node:assert/strict";
import { randomBytes } from "node:crypto";
import { preflight } from "./preflight";
import { requireFreePorts } from "./ports";
import { fixtures } from "./fixtures/store";
import { request } from "./client/http";
import { frames, binaryFrame } from "./client/frames";
import { UploadSocket } from "./client/socket";
import { serve } from "./service";
import { rustServer, ready } from "./processes";
import { Inspector } from "./inspection";
import { redisUrl } from "./config";
import { logs } from "./logs";

await preflight();
await requireFreePorts([5151, 5152]);
const service = serve(),
  inspector = new Inspector();
let rust = rustServer({ all: false });
let socket: UploadSocket | undefined;
const username = `e2e_release_${randomBytes(6).toString("hex")}`;
const password = randomBytes(24).toString("hex");
let aclCreated = false;
try {
  await inspector.redis.connect();
  await ready(rust);
  const item = (await fixtures())[0];
  const issued = await request("/api/v1/runs", "POST", {
    protocol_version: 1,
    client_game_version: "e2e-recorded",
    client_mod_version: "e2e-lifecycle-probe",
    tuf_level_id: item.levelId,
    client_tuf_file_id: item.fileId,
    client_installed_payload_hash_hex: item.chartSha,
    client_payload_hash_version: 1,
    client_level_relative_path: "main.adofai",
  });
  const id = issued.run_id,
    payload = await frames(item, "accepted", issued.max_chunk_bytes);
  const fresh = await inspector.snapshot("issued", id);
  assert.equal(fresh.hasManifest, false);
  assert.equal(fresh.chunks, 0);
  socket = new UploadSocket(id, issued.upload_token);
  await socket.open();
  socket.send({ type: "hello", protocol_version: 1, last_acknowledged_sequence: -1 });
  await socket.until("ready");
  for (let sequence = 0; sequence < payload.length; sequence++) {
    socket.binary(binaryFrame(payload[sequence], sequence));
    await socket.until("ack", sequence);
    if (sequence === 0) {
      const streaming = await inspector.snapshot("uploading", id);
      assert.equal(streaming.status, "streaming");
      assert.equal(streaming.chunks, 1);
    }
  }
  const complete = {
    type: "complete",
    final_sequence: payload.length - 1,
    input_count: item.inputCount,
    hit_context_count: item.hitCount,
  };
  socket.send(complete);
  await socket.until("sealed", payload.length - 1);
  socket.close();
  const sealed = await inspector.snapshot("sealed-before-worker", id);
  assert.equal(sealed.hasManifest, false);
  assert.equal(sealed.chunks, payload.length);
  // Temporary read-only identity: assembly succeeds, actual UNLINK is denied.
  await inspector.redis.send("ACL", [
    "SETUSER",
    username,
    "reset",
    "on",
    `>${password}`,
    "~e2e:*",
    "+@read",
    "+ping",
    "+client",
    "+select",
  ]);
  aclCreated = true;
  const denied = new URL(redisUrl);
  denied.username = username;
  denied.password = password;
  rust.kill("SIGTERM");
  await rust.exited;
  rust = rustServer({ redisUrl: denied.toString() });
  await ready(rust);
  await inspector.waitFor(id, (row) => row.state === "evidence_ready");
  const denialDeadline = Date.now() + 10000;
  while (!logs().some((event) => /NOPERM.*unlink/i.test(event.message))) {
    assert.ok(Date.now() < denialDeadline, "Redis must actually deny UNLINK before restart");
    await Bun.sleep(50);
  }
  const failed = await inspector.snapshot("manifest-committed-unlink-denied", id);
  assert.equal(failed.releasedAt, null);
  assert.equal(failed.chunks, payload.length);
  assert.ok(failed.files.length && failed.files.every((file: { exists: boolean }) => file.exists));
  rust.kill("SIGTERM");
  await rust.exited;
  rust = rustServer();
  await ready(rust);
  await inspector.waitFor(id, (row) => row.ingest_released_at !== null);
  const recovered = await inspector.snapshot("restart-reconciled", id);
  assert.equal(recovered.chunkTtl, -2);
  assert.equal(recovered.chunks, 0);
  assert.ok(recovered.metaTtl > 0);
  socket = new UploadSocket(id, issued.upload_token);
  await socket.open();
  socket.send({ type: "hello", protocol_version: 1, last_acknowledged_sequence: -1 });
  socket.send(complete);
  const acknowledgement = await socket.until("sealed", payload.length - 1);
  socket.close();
  assert.equal(acknowledgement.acknowledged_sequence, payload.length - 1);
  const receipt = await request(
    `/api/v1/runs/${id}/receipt`,
    "GET",
    undefined,
    issued.upload_token,
  );
  assert.equal(receipt.input_count, item.inputCount);
  assert.equal(receipt.hit_context_count, item.hitCount);
  await inspector.snapshot("complete-response-recovered", id);
  await request(`/api/v1/runs/${id}/submit`, "POST");
  await inspector.waitFor(id, (row) => row.state === "submitted");
  const submitted = await inspector.snapshot("submitted", id);
  assert.equal(submitted.chunkTtl, -2);
  assert.ok(submitted.metaTtl > 0);
  console.log(`Recorded ${inspector.observations.length} lifecycle observations`);
  console.log("PASS lifecycle inspection, actual Redis UNLINK denial and restart recovery");
} finally {
  socket?.close();
  rust.kill("SIGTERM");
  await rust.exited;
  await service.stop(true);
  if (aclCreated) await inspector.redis.send("ACL", ["DELUSER", username]);
  await inspector.save();
  await inspector.close();
}
