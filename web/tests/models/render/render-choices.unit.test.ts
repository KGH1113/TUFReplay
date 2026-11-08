import { describe, expect, test } from "bun:test";
import {
  bitrateForMegabytesPerMinute,
  describeFolder,
  megabytesPerMinute,
  qualityOf,
  readTypedNumber,
  settleBitrate,
  smoothnessChoices,
  withQuality,
  withVideoCodec,
  withVideoFps,
} from "@/models/render/render-choices";
import { defaultRenderOptions } from "@/models/render/render-model";
import { renderOptionsSchema } from "@/schemas/render/render-schema";

describe("render choices", () => {
  test("names plain video sizes and treats others as custom", () => {
    expect(qualityOf({ width: 1920, height: 1080 })).toBe("1080");
    expect(qualityOf({ width: 3840, height: 2160 })).toBe("2160");
    expect(qualityOf({ width: 1080, height: 1920 })).toBeNull();
    expect(withQuality(defaultRenderOptions, "1440")).toMatchObject({ width: 2560, height: 1440 });
  });

  test("keeps the game stepping at least as often as the video takes frames", () => {
    const slow = { ...defaultRenderOptions, simulationFps: 60, videoFps: 30 };
    const faster = withVideoFps(slow, 120);
    expect(faster).toMatchObject({ videoFps: 120, simulationFps: 120 });
    expect(renderOptionsSchema.safeParse(faster).success).toBe(true);
    expect(withVideoFps(defaultRenderOptions, 30).simulationFps).toBe(
      defaultRenderOptions.simulationFps,
    );
  });

  test("keeps encoding settings valid when the format changes", () => {
    const apple = { ...defaultRenderOptions, encoder: "AppleVideoToolbox" as const };
    const vp9 = withVideoCodec(apple, "VP9");
    expect(vp9.encoder).toBe("Software");
    expect(renderOptionsSchema.safeParse(vp9).success).toBe(true);
    expect(withVideoCodec(apple, "H265").encoder).toBe("AppleVideoToolbox");
    const proRes = withVideoCodec(defaultRenderOptions, "ProRes");
    expect(proRes.bitDepth).toBe(10);
    expect(renderOptionsSchema.safeParse(proRes).success).toBe(true);
  });

  test("offers today's frame rates and describes bitrate as file size", () => {
    expect([...smoothnessChoices]).toEqual([60, 120, 240]);
    for (const fps of smoothnessChoices)
      expect(renderOptionsSchema.safeParse(withVideoFps(defaultRenderOptions, fps)).success).toBe(
        true,
      );
    expect(megabytesPerMinute(18_000)).toBe(135);
  });

  test("turns a typed file size into the bitrate that makes it", () => {
    expect(bitrateForMegabytesPerMinute(150)).toBe(20_000);
    // 100 MB a minute needs 13,333.3 Kbps; it still reads back as 100.
    const typed = settleBitrate(bitrateForMegabytesPerMinute(100), 1);
    expect(typed).toBe(13_333);
    expect(megabytesPerMinute(typed)).toBe(100);
  });

  test("keeps a typed bitrate in range and on the renderer's steps", () => {
    expect(settleBitrate(18_500, 1)).toBe(18_500);
    // An OrbitRender that takes whole Mbps gets the nearest one, as the mod sends it.
    expect(settleBitrate(18_500, 1_000)).toBe(19_000);
    expect(settleBitrate(18_499, 1_000)).toBe(18_000);
    expect(settleBitrate(500, 1)).toBe(1_000);
    expect(settleBitrate(250_000, 1)).toBe(200_000);
  });

  test("reads numbers as people type them", () => {
    expect(readTypedNumber("18,500")).toBe(18_500);
    expect(readTypedNumber(" 2 500 ")).toBe(2_500);
    expect(readTypedNumber("12.5")).toBe(12.5);
    expect(readTypedNumber("")).toBeNull();
    expect(readTypedNumber("abc")).toBeNull();
    expect(readTypedNumber("-5")).toBeNull();
  });

  test("shows a folder by its own name", () => {
    expect(describeFolder("~/Videos/A Dance of Fire and Ice/TUFReplay/")).toEqual({
      name: "TUFReplay",
      parent: "~/Videos/A Dance of Fire and Ice",
      path: "~/Videos/A Dance of Fire and Ice/TUFReplay",
    });
    expect(describeFolder("C:\\Users\\me\\Videos")).toMatchObject({
      name: "Videos",
      parent: "C:\\Users\\me",
    });
    expect(describeFolder("/").name).toBe("");
  });
});
