import { afterEach, describe, expect, test } from "bun:test";
import {
  readBitrateView,
  readLastRenderOptions,
  saveBitrateView,
} from "@/hooks/render/render-option-storage";
import { defaultRenderOptions } from "@/models/render/render-model";

const values = new Map<string, string>();
Object.assign(globalThis, {
  localStorage: {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key),
  },
});

afterEach(() => values.clear());

describe("render option storage", () => {
  test("keeps a bitrate saved in Mbps before the move to Kbps", () => {
    values.set("tufreplay.render-options.v1", JSON.stringify({ bitrateMbps: 24 }));
    expect(readLastRenderOptions(defaultRenderOptions).bitrateKbps).toBe(24_000);
  });

  test("prefers a bitrate saved in Kbps", () => {
    values.set(
      "tufreplay.render-options.v1",
      JSON.stringify({ bitrateMbps: 24, bitrateKbps: 18_500 }),
    );
    expect(readLastRenderOptions(defaultRenderOptions).bitrateKbps).toBe(18_500);
  });

  test("remembers whether the bitrate shows as file size or Kbps", () => {
    expect(readBitrateView()).toBe("fileSize");
    saveBitrateView("bitrate");
    expect(readBitrateView()).toBe("bitrate");
  });
});
