import { describe, expect, test } from "bun:test";

import activityEn from "@/i18n/locales/en/activity.json";
import commonEn from "@/i18n/locales/en/common.json";
import microphoneEn from "@/i18n/locales/en/microphone.json";
import renderEn from "@/i18n/locales/en/render.json";
import replayEn from "@/i18n/locales/en/replay.json";
import webcamEn from "@/i18n/locales/en/webcam.json";
import activityKo from "@/i18n/locales/ko/activity.json";
import commonKo from "@/i18n/locales/ko/common.json";
import microphoneKo from "@/i18n/locales/ko/microphone.json";
import renderKo from "@/i18n/locales/ko/render.json";
import replayKo from "@/i18n/locales/ko/replay.json";
import webcamKo from "@/i18n/locales/ko/webcam.json";

describe("translation resources", () => {
  test.each([
    ["common", commonEn, commonKo],
    ["activity", activityEn, activityKo],
    ["microphone", microphoneEn, microphoneKo],
    ["replay", replayEn, replayKo],
    ["render", renderEn, renderKo],
    ["webcam", webcamEn, webcamKo],
  ])("keeps the %s key set in sync", (_namespace, english, korean) => {
    expect(leafKeys(korean)).toEqual(leafKeys(english));
  });
});

function leafKeys(value: unknown, prefix = ""): string[] {
  if (Array.isArray(value)) return value.map((_item, index) => `${prefix}.${index}`).sort();
  if (!value || typeof value !== "object") return [prefix];
  return Object.entries(value)
    .flatMap(([key, child]) => leafKeys(child, prefix ? `${prefix}.${key}` : key))
    .sort();
}
