import { describe, expect, test } from "bun:test";
import { fileURLToPath } from "node:url";

const layerRank: Record<string, number> = {
  ports: 0,
  shared: 0,
  i18n: 0,
  schemas: 1,
  models: 2,
  application: 3,
  api: 3,
  adapters: 4,
  state: 4,
  mocks: 4,
  hooks: 5,
  components: 6,
  sections: 7,
  pages: 8,
  app: 9,
};

const sourceRoot = new URL("../../src/", import.meta.url);

describe("web import boundaries", () => {
  test("production layers depend only on the same or lower layers", async () => {
    const violations: string[] = [];
    const glob = new Bun.Glob("**/*.{ts,tsx}");

    for await (const relativePath of glob.scan({ cwd: fileURLToPath(sourceRoot) })) {
      const sourceLayer = relativePath.split("/")[0];
      const sourceRank = layerRank[sourceLayer];
      if (sourceRank === undefined) continue;

      const source = await Bun.file(new URL(relativePath, sourceRoot)).text();
      for (const match of source.matchAll(/(?:from\s+|import\s*\()["']@\/([^"']+)/g)) {
        const target = match[1];
        const targetLayer = target.split("/")[0];
        const targetRank = layerRank[targetLayer];
        if (targetRank !== undefined && targetRank > sourceRank) {
          violations.push(`${relativePath}: ${sourceLayer} -> ${targetLayer} (${target})`);
        }
      }
    }

    expect(violations).toEqual([]);
  });

  test("shadcn primitives stay domain-agnostic", async () => {
    const violations: string[] = [];
    const glob = new Bun.Glob("shared/ui/*.{ts,tsx}");

    for await (const relativePath of glob.scan({ cwd: fileURLToPath(sourceRoot) })) {
      const source = await Bun.file(new URL(relativePath, sourceRoot)).text();
      for (const match of source.matchAll(/(?:from\s+|import\s*\()["']@\/([^"']+)/g)) {
        if (!match[1].startsWith("shared/")) {
          violations.push(`${relativePath}: ${match[1]}`);
        }
      }
    }

    expect(violations).toEqual([]);
  });

  test("components contain renderable TSX only", async () => {
    const files: string[] = [];
    const glob = new Bun.Glob("components/**/*.ts");

    for await (const relativePath of glob.scan({ cwd: fileURLToPath(sourceRoot) })) {
      files.push(relativePath);
    }

    expect(files.sort()).toEqual([]);
  });

  test("pages delegate state and effects to ViewModel hooks", async () => {
    const violations: string[] = [];
    const glob = new Bun.Glob("pages/**/*.tsx");

    for await (const relativePath of glob.scan({ cwd: fileURLToPath(sourceRoot) })) {
      const source = await Bun.file(new URL(relativePath, sourceRoot)).text();
      if (/\buse(?:State|Effect|LayoutEffect|Reducer|Memo|Callback|Ref)\s*\(/.test(source)) {
        violations.push(relativePath);
      }
    }

    expect(violations).toEqual([]);
  });

  test("application and domain depend on ports rather than transport or presentation frameworks", async () => {
    const violations: string[] = [];
    const glob = new Bun.Glob("{application,models,api}/**/*.{ts,tsx}");
    for await (const path of glob.scan({ cwd: fileURLToPath(sourceRoot) })) {
      if (path.replaceAll("\\", "/") === "api/app-api-provider.tsx") continue;
      const source = await Bun.file(new URL(path, sourceRoot)).text();
      for (const match of source.matchAll(/(?:from\s+|import\s*\()["']([^"']+)/g)) {
        const dependency = match[1];
        if (
          dependency.startsWith("@/adapters/") ||
          dependency.includes("vendor/adofai-ipc") ||
          dependency === "adofai-ipc" ||
          dependency === "adofai-ipc/transport" ||
          dependency === "@adofai-ipc/client" ||
          dependency === "react" ||
          dependency.startsWith("@tauri-apps/") ||
          dependency.startsWith("@tanstack/")
        )
          violations.push(`${path}: ${dependency}`);
      }
    }
    expect(violations).toEqual([]);
  });

  test("active recorder APIs use the generated methods and opaque file capabilities", async () => {
    const violations: string[] = [];
    const glob = new Bun.Glob("{api,adapters,ports,hooks}/**/*.{ts,tsx}");
    for await (const path of glob.scan({ cwd: fileURLToPath(sourceRoot) })) {
      // The independent Renderer remains an inactive, coming-soon integration.
      if (
        path.replaceAll("\\", "/").startsWith("api/render/") ||
        path.replaceAll("\\", "/") === "api/domain-messages.ts"
      )
        continue;
      const source = await Bun.file(new URL(path, sourceRoot)).text();
      if (
        /sendDomainCommand\(|\.namespace\.send\(/.test(source) ||
        source.includes("/ipc/download")
      )
        violations.push(path);
    }
    expect(violations).toEqual([]);
  });
});
