import { describe, expect, test } from "bun:test";
import { defaultRenderOptions, type RenderSettings } from "@/models/render/render-model";
import {
  applyRenderRecommendation,
  createRenderRecommendations,
} from "@/models/render/render-recommendations";
import { renderOptionsSchema } from "@/schemas/render/render-schema";

function settings(patch: Partial<NonNullable<RenderSettings["system"]>> = {}): RenderSettings {
  return {
    defaults: { ...defaultRenderOptions, outputDirectory: "/videos" },
    outputDirectory: "/videos",
    capabilities: {
      codecs: ["H264", "H265", "VP9", "AV1", "ProRes"],
      encoders: ["Auto", "Software", "NvidiaNvenc", "AmdAmf", "IntelQsv", "AppleVideoToolbox"],
      bitDepths: [8, 10],
      proResProfiles: ["HQ"],
      pixelFormats: ["auto", "yuv420p"],
    },
    system: {
      platform: "windows",
      processorName: "AMD Ryzen",
      logicalProcessors: 16,
      memoryMb: 32768,
      graphicsName: "NVIDIA RTX",
      graphicsMemoryMb: 8192,
      maxTextureSize: 16384,
      encoding: { state: "ready", h264Encoders: ["Software", "NvidiaNvenc"] },
      ...patch,
    },
  };
}

describe("render recommendations from the game computer", () => {
  test("offers six valid distinct quality profiles and never defaults to extreme", () => {
    const recommendations = createRenderRecommendations(settings());
    expect(recommendations.levels).toHaveLength(6);
    expect(recommendations.recommended).toBe("highest");
    const profiles = recommendations.levels.map((level) => {
      expect(
        renderOptionsSchema.safeParse({ ...defaultRenderOptions, ...level.video }).success,
      ).toBe(true);
      expect(level.video.simulationFps).toBeGreaterThanOrEqual(level.video.videoFps);
      return `${level.video.width}:${level.video.height}:${level.video.videoFps}`;
    });
    expect(new Set(profiles).size).toBe(6);
  });

  test("unverified NVIDIA names never select an accelerator during first-use installation", () => {
    for (const state of ["checking", "missing", "unavailable"] as const) {
      const recommendations = createRenderRecommendations(
        settings({ encoding: { state, h264Encoders: ["NvidiaNvenc"] } }),
      );
      expect(recommendations.encoder).toBe("Software");
      expect(recommendations.recommended).toBe("medium");
    }
  });

  test("the Apple computer uses verified H.264 VideoToolbox and unified memory", () => {
    const recommendations = createRenderRecommendations(
      settings({
        platform: "macos",
        processorName: "Apple M4 Pro",
        graphicsName: "Apple M4 Pro",
        logicalProcessors: 12,
        memoryMb: 24576,
        graphicsMemoryMb: 0,
        encoding: { state: "ready", h264Encoders: ["Software", "AppleVideoToolbox"] },
      }),
    );
    expect(recommendations.encoder).toBe("AppleVideoToolbox");
    expect(recommendations.recommended).toBe("high");
  });

  test("honors the engine's offered encoders even if the probe reports another backend", () => {
    const configured = settings();
    configured.engineOptions = { codecs: [{ value: "H264", encoders: ["Software"] }] };
    expect(createRenderRecommendations(configured).encoder).toBe("Software");
  });

  test("reduces the default on limited hardware and disables resolutions beyond texture limits", () => {
    const recommendations = createRenderRecommendations(
      settings({ logicalProcessors: 2, memoryMb: 3072, maxTextureSize: 2048 }),
    );
    expect(recommendations.recommended).toBe("lowest");
    expect(recommendations.levels.find((value) => value.quality === "medium")?.supported).toBe(
      true,
    );
    expect(recommendations.levels.find((value) => value.quality === "high")?.supported).toBe(false);
    expect(createRenderRecommendations(settings({ maxTextureSize: 2048 })).recommended).toBe(
      "medium",
    );
  });

  test("uses a valid conservative profile with an older Renderer that omits system details", () => {
    const configured = settings();
    delete configured.system;
    const recommendations = createRenderRecommendations(configured);
    expect(recommendations.encoder).toBe("Software");
    expect(recommendations.recommended).toBe("medium");
    expect(
      renderOptionsSchema.safeParse(
        applyRenderRecommendation(defaultRenderOptions, recommendations, null),
      ).success,
    ).toBe(true);
  });

  test("installation refresh preserves the chosen tier, folder, recordings and death settings", () => {
    const custom = {
      ...defaultRenderOptions,
      videoCodec: "ProRes" as const,
      bitDepth: 10 as const,
      outputDirectory: "/external drive/my videos",
      endDelaySeconds: 7,
      audioGainDb: -6,
      includeWebcam: false,
      includeMicrophone: false,
      includeDmNote: false,
      bgaMode: true,
    };
    const waiting = createRenderRecommendations(
      settings({ encoding: { state: "missing", h264Encoders: ["Software"] } }),
    );
    const ready = createRenderRecommendations(settings());
    const before = applyRenderRecommendation(custom, waiting, "low");
    const after = applyRenderRecommendation(custom, ready, "low");
    expect(before.encoder).toBe("Software");
    expect(after.encoder).toBe("NvidiaNvenc");
    for (const result of [before, after]) {
      expect(result).toMatchObject({
        width: 1280,
        height: 720,
        videoFps: 60,
        videoCodec: "H264",
        bitDepth: 8,
        outputDirectory: custom.outputDirectory,
        endDelaySeconds: 7,
        audioGainDb: -6,
        includeWebcam: false,
        includeMicrophone: false,
        includeDmNote: false,
        bgaMode: true,
      });
    }
    expect(custom.videoCodec).toBe("ProRes");
  });
});
