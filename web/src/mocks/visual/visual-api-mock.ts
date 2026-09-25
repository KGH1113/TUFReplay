import type { VisualApi, VisualPresetImport } from "@/api/visual/visual-api";
import type { VisualPreset, VisualPresetPage, VisualSources } from "@/models/visual/visual-model";

export function createVisualApiMock(): VisualApi {
  let nextId = 1;
  let presets: VisualPreset[] = [
    {
      id: "mock-keyviewer-jipper",
      name: "Aurora keys",
      kind: "keyviewer",
      source: "jipper-resourcepack",
      source_version: "1.5.2.0",
      created_at: "2026-09-16T00:00:00Z",
    },
  ];

  const sources: VisualSources = {
    sources: [
      {
        source: "jipper-resourcepack",
        version: "1.5.2.0",
        available: true,
        kinds: ["keyviewer", "overlay"],
      },
      { source: "impl-dmnote", version: "0.1.0", available: true, kinds: ["keyviewer"] },
      { source: "jipper-keyviewer", version: "1.7.2", available: true, kinds: ["keyviewer"] },
      { source: "impl-resourcepack", version: "0.1.0", available: true, kinds: ["overlay"] },
      { source: "dmnote", version: "1", available: true, kinds: ["keyviewer"] },
    ],
  };

  return {
    async listPresets(): Promise<VisualPresetPage> {
      return { presets: presets.map((preset) => ({ ...preset })) };
    },
    async getSources() {
      return sources;
    },
    async inspectPreset() {
      return { missing_assets: [], asset_count: 0 };
    },
    async registerPreset(input, onProgress) {
      const progress = { stage: "uploading" as const, completed_assets: 0 };
      onProgress?.(progress);
      return { state: "completed", progress, preset: await this.importPreset(input) };
    },
    async importPreset(input: VisualPresetImport) {
      const preset: VisualPreset = {
        id: `mock-visual-${nextId++}`,
        name: input.name.trim(),
        kind: input.kind,
        source: input.source,
        source_version:
          sources.sources.find((source) => source.source === input.source)?.version ?? "1",
        created_at: new Date().toISOString(),
      };
      presets = [...presets, preset];
      return preset;
    },
    async removePreset(id) {
      if (!presets.some((preset) => preset.id === id)) throw new Error("visual_preset_not_found");
      presets = presets.filter((preset) => preset.id !== id);
    },
    async renamePreset(id, name) {
      const preset = presets.find((item) => item.id === id);
      if (!preset) throw new Error("visual_preset_not_found");
      const cleanName = name.trim();
      if (!cleanName) throw new Error("visual_name_required");
      if (
        presets.some(
          (item) => item.id !== id && item.name.toLowerCase() === cleanName.toLowerCase(),
        )
      )
        throw new Error("visual_name_taken");
      const renamed = { ...preset, name: cleanName };
      presets = presets.map((item) => (item.id === id ? renamed : item));
      return { ...renamed };
    },
  };
}
