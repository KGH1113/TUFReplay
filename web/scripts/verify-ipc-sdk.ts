import { createHash } from "node:crypto";
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { z } from "zod";
import { CLIENT_VERSION, PROTOCOL_VERSION } from "../vendor/adofai-ipc/src/version";

const vendorDirectory = fileURLToPath(new URL("../vendor/adofai-ipc/", import.meta.url));
const manifestSchema = z
  .object({
    source: z.literal("https://github.com/KGH1113/adofai-ipc"),
    sourceRevision: z.string().regex(/^[a-f0-9]{40}$/),
    sourceDirty: z.literal(false),
    version: z.literal(CLIENT_VERSION),
    protocolVersion: z.literal(PROTOCOL_VERSION),
    files: z.record(z.string().regex(/^[a-z][a-z0-9-]*\.ts$/), z.string().regex(/^[a-f0-9]{64}$/)),
  })
  .strict();

/** Verify the committed SDK snapshot without consulting npm or a sibling checkout. */
export function verifyIpcSdk(directory = vendorDirectory) {
  const manifest = manifestSchema.parse(
    JSON.parse(readFileSync(join(directory, "source.json"), "utf8")),
  );
  const expected = Object.keys(manifest.files).sort();
  const actual = readdirSync(join(directory, "src")).sort();
  if (!expected.includes("version.ts") || JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error(
      "ADOFAI-IPC SDK source inventory differs from source.json. Regenerate with client-sync.",
    );
  }
  for (const name of expected) {
    const hash = createHash("sha256")
      .update(readFileSync(join(directory, "src", name)))
      .digest("hex");
    if (hash !== manifest.files[name]) {
      throw new Error(`ADOFAI-IPC SDK hash mismatch: ${name}. Regenerate with client-sync.`);
    }
  }
  if (readFileSync(join(directory, "LICENSE"), "utf8").trim().length === 0) {
    throw new Error("ADOFAI-IPC SDK license is missing.");
  }
  return manifest;
}

if (import.meta.main) {
  const manifest = verifyIpcSdk();
  if (process.argv.includes("--write-metadata")) {
    const publicDirectory = fileURLToPath(new URL("../public/", import.meta.url));
    mkdirSync(publicDirectory, { recursive: true });
    writeFileSync(
      join(publicDirectory, "adofai-ipc.json"),
      `${JSON.stringify(manifest, null, 2)}\n`,
    );
  }
  console.log(
    `Verified ADOFAI-IPC ${manifest.version}, protocol ${manifest.protocolVersion}, source ${manifest.sourceRevision}.`,
  );
}
