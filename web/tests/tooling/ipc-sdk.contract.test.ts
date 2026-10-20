import { afterEach, describe, expect, test } from "bun:test";
import { cpSync, mkdtempSync, readFileSync, rmSync, unlinkSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { verifyIpcSdk } from "../../scripts/verify-ipc-sdk";

const require = createRequire(import.meta.url);
const source = fileURLToPath(new URL("../../vendor/adofai-ipc/", import.meta.url));
const installed = dirname(dirname(require.resolve("adofai-ipc")));
const lock = fileURLToPath(new URL("../../../bun.lock", import.meta.url));
const fixtures: string[] = [];
function snapshot() {
  const directory = mkdtempSync(join(tmpdir(), "tufreplay-ipc-sdk-"));
  fixtures.push(directory);
  cpSync(source, join(directory, "metadata"), { recursive: true });
  cpSync(installed, join(directory, "package"), { recursive: true });
  cpSync(lock, join(directory, "bun.lock"));
  return {
    metadata: join(directory, "metadata"),
    package: join(directory, "package"),
    lock: join(directory, "bun.lock"),
    verify: () =>
      verifyIpcSdk(
        join(directory, "metadata"),
        join(directory, "package"),
        join(directory, "bun.lock"),
      ),
  };
}
afterEach(() => {
  for (const directory of fixtures.splice(0)) rmSync(directory, { recursive: true });
});

describe("published IPC SDK build contract", () => {
  test("the installed package verifies without contacting npm or another checkout", () => {
    expect(snapshot().verify()).toMatchObject({
      version: "2.1.0",
      protocolVersion: 3,
      sourceDirty: false,
    });
  });
  test("modified package bytes cannot be deployed with stale provenance", () => {
    const fixture = snapshot();
    writeFileSync(join(fixture.package, "dist/index.js"), "export const stale = true;");
    expect(fixture.verify).toThrow("hash mismatch: dist/index.js");
  });
  test.each(["missing", "extra"])("rejects %s installed files", (kind) => {
    const fixture = snapshot();
    if (kind === "missing") unlinkSync(join(fixture.package, "dist/index.js"));
    else writeFileSync(join(fixture.package, "dist/untracked.js"), "export {};");
    expect(fixture.verify).toThrow("package inventory");
  });
  test("rejects a lockfile with different tarball integrity", () => {
    const fixture = snapshot();
    const lock = Bun.JSONC.parse(readFileSync(fixture.lock, "utf8")) as {
      packages: Record<string, unknown[]>;
    };
    lock.packages["adofai-ipc"][3] = "sha512-wrong";
    writeFileSync(fixture.lock, JSON.stringify(lock));
    expect(fixture.verify).toThrow("registry integrity");
  });
  test.each([
    { sourceDirty: true },
    { version: "2.0.0" },
    { protocolVersion: 2 },
  ])("rejects incompatible provenance %j", (change) => {
    const fixture = snapshot();
    const path = join(fixture.metadata, "source.json");
    writeFileSync(path, JSON.stringify({ ...JSON.parse(readFileSync(path, "utf8")), ...change }));
    expect(fixture.verify).toThrow();
  });
});
