import { describe, expect, test } from "bun:test";

import { formatDayLabel, formatTime } from "./activity-date.utils";

describe("localized activity dates", () => {
  test("formats a day using the requested locale", () => {
    expect(formatDayLabel("2026-07-29", "en-US")).toBe("Jul 29");
    expect(formatDayLabel("2026-07-29", "ko-KR")).toBe("7월 29일");
  });

  test("uses the caller-provided empty time label", () => {
    expect(formatTime(null, "ko-KR", "진행 중", "Asia/Seoul")).toBe("진행 중");
  });
});
