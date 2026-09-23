import { createHash } from "node:crypto";
import { join } from "node:path";
import { SQL } from "bun";
import { databaseUrl, owner, requireLocal } from "../config";

// Read the actual locally registered immutable snapshot, without altering a submitted run.
const name = process.argv[2];
if (!name || process.env.E2E_TUF_TARGET !== "local") throw new Error("Local E2E and preset name required");
requireLocal(databaseUrl);
const sql = new SQL(databaseUrl);
try {
  const rows = await sql`SELECT id,name,source,source_version,bundle,sha256,bytes FROM visual_presets WHERE owner_id=${owner} AND name=${name} AND kind='keyviewer' AND deleted_at IS NULL`;
  if (rows.length !== 1) throw new Error("Expected exactly one registered preset");
  const row = rows[0];
  const bytes = Buffer.from(row.bundle);
  if (createHash("sha256").update(bytes).digest("hex") !== row.sha256) throw new Error("Stored checksum mismatch");
  const root = "/Users/kgh/dev/src/adofai-web-editor/public/test-creplay/asgore";
  const manifest = await Bun.file(join(root, "visuals.json")).json();
  manifest.keyviewer = { preset_id: row.id, name: row.name, source: row.source, source_version: row.source_version, sha256: row.sha256, bytes: Number(row.bytes), url: "/test-creplay/asgore/visuals/keyviewer" };
  await Bun.write(join(root, "keyviewer.json"), bytes);
  await Bun.write(join(root, "visuals/keyviewer"), bytes);
  await Bun.write(join(root, "visuals.json"), JSON.stringify(manifest));
  const preset = JSON.parse(bytes.toString()).files["preset.json"];
  const css = await Bun.file("/Users/kgh/games/adofai/adofai-configs/dmnote/custom2.css").text();
  console.log({ id: row.id, name: row.name, placement: preset.tufReplayPlacement, keyCounterEnabled: preset.keyCounterEnabled, customCssMatches: preset.customCSS?.content === css, verifiedBytes: bytes.length });
} finally {
  await sql.close();
}
