import { expect, test } from "bun:test";
import en from "@/i18n/locales/en/activity.json";
import ko from "@/i18n/locales/ko/activity.json";
import { activityRunDtoSchema } from "@/schemas/activity/activity-schema";

test("accepts capture failure reasons and explains why the saved run cannot replay in both languages", () => {
  const reasons = [
    "input_queue_overflow",
    "input_tap_timeout",
    "input_tap_disabled",
    "input_event_delayed",
    "input_source_stopped",
    "input_permission_denied",
    "input_start_failed",
    "input_read_failed",
  ] as const;
  for (const reason of reasons) {
    expect(activityRunDtoSchema.shape.ReplayUnavailableReason.parse(reason)).toBe(reason);
    expect(en.run.replayUnavailable[reason]).toContain("saved");
    expect(ko.run.replayUnavailable[reason]).toContain("저장");
    expect(ko.run.replayUnavailable[reason]).toContain("재생");
  }
});
