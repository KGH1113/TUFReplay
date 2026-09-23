import { expect, test } from "bun:test";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { visualSourceSchema } from "../../web/src/schemas/visual/visual-schema";

const root = resolve(import.meta.dir, "../..");
const playerRoot = resolve(root, "../adofai-web-editor");
const frontendRoot = resolve(root, "../t21c-web-frontend");

test("mod, companion, server and player agree on sources while TUF host stays source-agnostic", async () => {
  const definitions = await readFile(
    resolve(root, "TUFReplay/Visual/Domain/VisualSourceDefinition.cs"),
    "utf8",
  );
  const sources = Object.fromEntries(
    [
      ...definitions.matchAll(
        /new VisualSourceDefinition\(\s*VisualSource\.\w+,\s*"([^"]+)"([\s\S]*?)\)/g,
      ),
    ].map((match) => [
      match[1],
      [...match[2].matchAll(/VisualKind\.(Keyviewer|Overlay)/g)].map((kind) =>
        kind[1].toLowerCase(),
      ),
    ]),
  );
  expect(Object.keys(sources).length).toBeGreaterThan(0);
  expect([...visualSourceSchema.options].sort()).toEqual(Object.keys(sources).sort());
  const rust = await readFile(resolve(root, "server/src/models/visual_presets.rs"), "utf8");
  const serverSources = [...rust.matchAll(/Self::\w+ => "([a-z-]+)"/g)]
    .map((match) => match[1])
    .filter((value) => value !== "keyviewer" && value !== "overlay");
  expect([...new Set(serverSources)].sort()).toEqual(Object.keys(sources).sort());
  const host = await import(
    pathToFileURL(
      resolve(frontendRoot, "src/pages/common/Pass/PassDetailPage/replay/replayDelivery.js"),
    ).href
  );
  const player = await import(
    pathToFileURL(resolve(playerRoot, "src/replay/replay-visual.model.ts")).href
  );
  const parser = await import(
    pathToFileURL(resolve(playerRoot, "src/replay/replay-visual-bundle.parser.ts")).href
  );
  expect(host.visualSourceKinds).toBeUndefined();
  expect(host.replayOpenPayload({ autoSubmissionRunId: "00000000-0000-4000-8000-000000000001", id: 1, levelId: 2 })).toEqual({ runId: "00000000-0000-4000-8000-000000000001", passId: 1, levelId: 2 });
  expect(player.REPLAY_VISUAL_SOURCE_KINDS).toEqual(sources);
  for (const obsolete of ["jipper", "quartz"]) {
    expect(visualSourceSchema.safeParse(obsolete).success).toBe(false);
    expect(obsolete in player.REPLAY_VISUAL_SOURCE_KINDS).toBe(false);
  }
  for (const [source, kinds] of Object.entries(sources)) {
    const descriptor = {
      preset_id: "00000000-0000-4000-8000-000000000001",
      name: "contract",
      source,
      source_version: "1",
      url: "/visuals/keyviewer",
      sha256: "a".repeat(64),
      bytes: 1,
    };
    expect(parser.parseReplayVisualDescriptor(descriptor).source).toBe(source);
    for (const kind of kinds) {
      expect(
        parser.parseReplayVisualBundle({
          schema_version: 1,
          source,
          kind,
          source_version: "1",
          viewport: { width: 1920, height: 1080 },
          files: {},
          assets: [],
        }).source,
      ).toBe(source);
    }
  }
});
