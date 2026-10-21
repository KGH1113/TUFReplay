import { createHash } from "node:crypto";
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { CLIENT_VERSION, PROTOCOL_VERSION } from "adofai-ipc/transport";
import { z } from "zod";

const require = createRequire(import.meta.url);
const metadataDirectory = fileURLToPath(new URL("../vendor/adofai-ipc/", import.meta.url));
const installedDirectory = dirname(dirname(require.resolve("adofai-ipc")));
const lockPath = fileURLToPath(new URL("../../bun.lock", import.meta.url));
const manifestSchema = z
  .object({
    source: z.literal("https://github.com/KGH1113/adofai-ipc"),
    sourceRevision: z.string().regex(/^[a-f0-9]{40}$/),
    sourceDirty: z.literal(false),
    version: z.literal("2.1.0"),
    protocolVersion: z.literal(3),
    registry: z.literal("https://registry.npmjs.org/"),
    integrity: z.string().regex(/^sha512-[A-Za-z0-9+/]+=*$/),
    files: z.record(
      z.string().regex(/^(?:dist\/[A-Za-z0-9._/-]+|package\.json|README\.md|LICENSE)$/),
      z.string().regex(/^[a-f0-9]{64}$/),
    ),
  })
  .strict();

function inventory(directory: string, prefix = ""): string[] {
  return readdirSync(directory, { withFileTypes: true })
    .flatMap((entry) => {
      const name = `${prefix}${entry.name}`;
      return entry.isDirectory() ? inventory(join(directory, entry.name), `${name}/`) : [name];
    })
    .sort();
}

/** Verify the installed registry package against committed hashes and the frozen lockfile. */
export function verifyIpcSdk(
  directory = metadataDirectory,
  packageDirectory = installedDirectory,
  dependencyLock = lockPath,
) {
  const manifest = manifestSchema.parse(
    JSON.parse(readFileSync(join(directory, "source.json"), "utf8")),
  );
  const declared = JSON.parse(
    readFileSync(fileURLToPath(new URL("../package.json", import.meta.url)), "utf8"),
  );
  const installed = JSON.parse(readFileSync(join(packageDirectory, "package.json"), "utf8"));
  const lock = z
    .object({ packages: z.record(z.string(), z.unknown()) })
    .parse(Bun.JSONC.parse(readFileSync(dependencyLock, "utf8")));
  const locked = lock.packages?.["adofai-ipc"];
  if (
    declared.dependencies["adofai-ipc"] !== manifest.version ||
    installed.name !== "adofai-ipc" ||
    installed.version !== manifest.version ||
    CLIENT_VERSION !== manifest.version ||
    PROTOCOL_VERSION !== manifest.protocolVersion
  )
    throw new Error("ADOFAI-IPC version differs from the pinned registry release.");
  if (
    !Array.isArray(locked) ||
    locked[0] !== `adofai-ipc@${manifest.version}` ||
    locked.at(-1) !== manifest.integrity
  )
    throw new Error("ADOFAI-IPC registry integrity differs from bun.lock.");

  const expected = Object.keys(manifest.files).sort();
  if (
    !expected.includes("dist/index.js") ||
    !expected.includes("LICENSE") ||
    expected.some((name) => name.split("/").includes("..")) ||
    JSON.stringify(inventory(packageDirectory)) !== JSON.stringify(expected)
  )
    throw new Error("ADOFAI-IPC package inventory differs from the verified registry tarball.");
  for (const name of expected) {
    const hash = createHash("sha256")
      .update(readFileSync(join(packageDirectory, name)))
      .digest("hex");
    if (hash !== manifest.files[name])
      throw new Error(`ADOFAI-IPC package hash mismatch: ${name}.`);
  }
  return manifest;
}

if (import.meta.main) {
  const manifest = verifyIpcSdk();
  if (process.argv.includes("--write-metadata")) {
    const directory = fileURLToPath(new URL("../public/", import.meta.url));
    mkdirSync(directory, { recursive: true });
    writeFileSync(join(directory, "adofai-ipc.json"), `${JSON.stringify(manifest, null, 2)}\n`);
  }
  console.log(
    `Verified npm ADOFAI-IPC ${manifest.version}, protocol ${manifest.protocolVersion}, source ${manifest.sourceRevision}.`,
  );
}
