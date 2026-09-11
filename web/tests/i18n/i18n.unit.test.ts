import { beforeAll, describe, expect, test } from "bun:test";

import i18n, { initializeI18n } from "@/i18n/i18n";

describe("i18n behavior", () => {
  beforeAll(initializeI18n);

  test("applies English plurals and Korean count labels", () => {
    const english = i18n.getFixedT("en", "activity");
    const korean = i18n.getFixedT("ko", "activity");

    expect(english("counts.runs", { count: 1 })).toBe("run");
    expect(english("counts.runs", { count: 2 })).toBe("runs");
    expect(korean("counts.runs", { count: 2 })).toBe("플레이");
    expect(korean("metadata.levelNumber", { levelId: 871 })).toBe("레벨 #871");
  });

  test("uses the approved Korean game terminology", () => {
    const activity = i18n.getFixedT("ko", "activity");
    const replay = i18n.getFixedT("ko", "replay");

    expect(activity("run.xAccuracy")).toBe("절대정확도");
    expect(activity("run.noFail")).toBe("무적모드");
    expect(activity("judgments", { returnObjects: true })).toEqual({
      Overload: "과부하",
      TooEarly: "매우빠름",
      Early: "빠름",
      EarlyPerfect: "약간빠름",
      Perfect: "정확",
      LatePerfect: "약간느림",
      Late: "느림",
      TooLate: "매우느림",
      Miss: "놓침",
    });
    expect(replay("status.sending")).toContain("얼불춤");
  });

  test("falls back to English for an unsupported language", () => {
    expect(i18n.getFixedT("ja", "common")("actions.save")).toBe("Save");
  });

  test("explains permanently unavailable replays in English and Korean", () => {
    const english = i18n.getFixedT("en", "activity");
    const korean = i18n.getFixedT("ko", "activity");

    expect(english("run.replayUnavailable.legacy_engine")).toContain("previous replay engine");
    expect(english("run.replayUnavailable.capture_incomplete")).toContain("incomplete");
    expect(korean("run.replayUnavailable.legacy_engine")).toContain("이전 리플레이 엔진");
    expect(korean("run.replayUnavailable.capture_incomplete")).toContain("완전하지 않습니다");
  });
});
