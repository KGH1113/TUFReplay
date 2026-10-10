import { afterEach, describe, expect, test } from "bun:test";
import { cpSync, mkdtempSync, readFileSync, rmSync, unlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { verifyIpcSdk } from "../../scripts/verify-ipc-sdk";

const source = fileURLToPath(new URL("../../vendor/adofai-ipc/", import.meta.url));
const fixtures: string[] = [];
function snapshot() {
  const directory = mkdtempSync(join(tmpdir(), "tufreplay-ipc-sdk-"));
  fixtures.push(directory);
  cpSync(source, directory, { recursive: true });
  return directory;
}
afterEach(() => {
  for (const directory of fixtures.splice(0)) rmSync(directory, { recursive: true });
});

describe("unpublished IPC SDK build contract", () => {
  test("the checked-in snapshot verifies without upstream files or npm", () => {
    expect(verifyIpcSdk(snapshot())).toMatchObject({
      version: "2.0.0",
      protocolVersion: 3,
      sourceDirty: false,
    });
  });
  test("a changed source file cannot be deployed with stale provenance", () => {
    const directory = snapshot();
    writeFileSync(join(directory, "src/client.ts"), "export const stale = true;");
    expect(() => verifyIpcSdk(directory)).toThrow("hash mismatch: client.ts");
  });
  test.each(["missing", "extra"])("rejects %s SDK source files", (kind) => {
    const directory = snapshot();
    if (kind === "missing") unlinkSync(join(directory, "src/client.ts"));
    else writeFileSync(join(directory, "src/untracked.ts"), "export {};");
    expect(() => verifyIpcSdk(directory)).toThrow("source inventory");
  });
  test.each([
    { sourceDirty: true },
    { version: "0.4.1" },
    { protocolVersion: 2 },
  ])("rejects uncommitted or incompatible provenance %j", (change) => {
    const directory = snapshot();
    const manifest = JSON.parse(readFileSync(join(directory, "source.json"), "utf8"));
    writeFileSync(join(directory, "source.json"), JSON.stringify({ ...manifest, ...change }));
    expect(() => verifyIpcSdk(directory)).toThrow();
  });
});
