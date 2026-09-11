import { describe, expect, test } from "bun:test";

const layerRank: Record<string, number> = {
  shared: 0,
  i18n: 0,
  schemas: 1,
  models: 2,
  api: 3,
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

    for await (const relativePath of glob.scan({ cwd: sourceRoot.pathname })) {
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

    for await (const relativePath of glob.scan({ cwd: sourceRoot.pathname })) {
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

    for await (const relativePath of glob.scan({ cwd: sourceRoot.pathname })) {
      files.push(relativePath);
    }

    expect(files.sort()).toEqual([]);
  });

  test("pages delegate state and effects to ViewModel hooks", async () => {
    const violations: string[] = [];
    const glob = new Bun.Glob("pages/**/*.tsx");

    for await (const relativePath of glob.scan({ cwd: sourceRoot.pathname })) {
      const source = await Bun.file(new URL(relativePath, sourceRoot)).text();
      if (/\buse(?:State|Effect|LayoutEffect|Reducer|Memo|Callback|Ref)\s*\(/.test(source)) {
        violations.push(relativePath);
      }
    }

    expect(violations).toEqual([]);
  });
});
