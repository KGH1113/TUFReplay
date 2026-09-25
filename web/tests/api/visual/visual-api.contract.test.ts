import { describe, expect, it } from "bun:test";
import { createVisualApi } from "@/api/visual/create-visual-api";
import type { AdofaiIpcClients } from "@/shared/clients/adofai-ipc-client";

const preset = {
  id: "visual-preset-1",
  name: "Aurora keys",
  kind: "keyviewer" as const,
  source: "jipper-resourcepack" as const,
  source_version: "1.5.2.0",
  created_at: "2026-09-16T00:00:00Z",
};

describe("visual preset IPC API", () => {
  it("validates and forwards list, source, import, rename, and remove calls", async () => {
    const calls: { method: string; params: unknown }[] = [];
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        if (method === "visual.presets.list") return { presets: [preset] };
        if (method === "visual.sources.get") {
          return {
            sources: [
              {
                source: "jipper-resourcepack",
                version: "1.5.2.0",
                available: true,
                kinds: ["keyviewer", "overlay"],
              },
              { source: "dmnote", version: "1", available: true, kinds: ["keyviewer"] },
            ],
          };
        }
        if (method === "visual.presets.import") return { preset };
        if (method === "visual.presets.rename") return { preset: { ...preset, name: "새 이름" } };
        if (method === "visual.presets.remove") return { deleted: true };
        throw new Error(`unexpected method ${method}`);
      },
    };
    const api = createVisualApi({
      namespace,
      pickerNamespace: namespace,
    } as unknown as AdofaiIpcClients);

    await expect(api.listPresets()).resolves.toEqual({ presets: [preset] });
    const sources = await api.getSources();
    expect(sources.sources[0]?.source).toBe("jipper-resourcepack");
    await expect(
      api.importPreset({
        name: "DMNote tab",
        kind: "keyviewer",
        source: "dmnote",
        presetJson: '{"tabs":[{"name":"main"}]}',
      }),
    ).resolves.toEqual(preset);
    await expect(api.renamePreset("visual-preset-1", " 새 이름 ")).resolves.toEqual({
      ...preset,
      name: "새 이름",
    });
    await expect(api.removePreset("visual-preset-1")).resolves.toBeUndefined();

    expect(calls).toContainEqual({ method: "visual.presets.list", params: {} });
    expect(calls).toContainEqual({ method: "visual.sources.get", params: {} });
    expect(calls).toContainEqual({
      method: "visual.presets.import",
      params: {
        name: "DMNote tab",
        kind: "keyviewer",
        source: "dmnote",
        presetJson: '{"tabs":[{"name":"main"}]}',
      },
    });
    expect(calls).toContainEqual({
      method: "visual.presets.rename",
      params: { id: "visual-preset-1", name: "새 이름" },
    });
    expect(calls).toContainEqual({
      method: "visual.presets.remove",
      params: { id: "visual-preset-1" },
    });
  });

  it("preserves new source identities and only sends exported presets for Impl DMNote", async () => {
    const calls: Record<string, unknown>[] = [];
    const namespace = {
      call: async (_method: string, params: Record<string, unknown>) => {
        calls.push(params);
        return { preset: { ...preset, source: params.source, kind: params.kind } };
      },
    };
    const api = createVisualApi({ namespace } as unknown as AdofaiIpcClients);
    for (const source of ["impl-dmnote", "jipper-keyviewer", "impl-resourcepack"] as const) {
      const result = await api.importPreset({
        name: "New source",
        source,
        kind: source === "impl-resourcepack" ? "overlay" : "keyviewer",
        presetJson: '{"keys":{"one":[]}}',
      });
      expect(result.source).toBe(source);
    }
    expect(calls[0]?.presetJson).toBe('{"keys":{"one":[]}}');
    expect(calls[1]).not.toHaveProperty("presetJson");
    expect(calls[2]).not.toHaveProperty("presetJson");
  });

  it("does not send an absent DMNote payload for game-owned sources", async () => {
    const calls: { method: string; params: unknown }[] = [];
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        return { preset };
      },
    };
    const api = createVisualApi({
      namespace,
      pickerNamespace: namespace,
    } as unknown as AdofaiIpcClients);

    await api.importPreset({
      name: "Jipper overlay",
      kind: "overlay",
      source: "jipper-resourcepack",
    });

    // Stale forms must not override the mod's detected on-disk settings.
    await api.importPreset({
      name: "Jipper keys",
      kind: "keyviewer",
      source: "jipper-resourcepack",
      presetJson: "{}",
    });

    expect(calls[0]?.params).toEqual({
      name: "Jipper overlay",
      kind: "overlay",
      source: "jipper-resourcepack",
    });
    expect(calls[1]?.params).toEqual({
      name: "Jipper keys",
      kind: "keyviewer",
      source: "jipper-resourcepack",
    });
  });
});
