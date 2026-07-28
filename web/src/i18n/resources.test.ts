import { describe, expect, test } from "bun:test";

import activityEn from "./locales/en/activity.json";
import commonEn from "./locales/en/common.json";
import microphoneEn from "./locales/en/microphone.json";
import replayEn from "./locales/en/replay.json";
import activityKo from "./locales/ko/activity.json";
import commonKo from "./locales/ko/common.json";
import microphoneKo from "./locales/ko/microphone.json";
import replayKo from "./locales/ko/replay.json";

describe("translation resources", () => {
  test.each([
    ["common", commonEn, commonKo],
    ["activity", activityEn, activityKo],
    ["microphone", microphoneEn, microphoneKo],
    ["replay", replayEn, replayKo],
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
