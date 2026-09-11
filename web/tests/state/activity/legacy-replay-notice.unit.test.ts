import { describe, expect, test } from "bun:test";
import {
  acknowledgeLegacyReplayNotice,
  hasAcknowledgedLegacyReplayNotice,
  LEGACY_REPLAY_NOTICE_STORAGE_KEY,
} from "@/state/activity/legacy-replay-notice";

describe("legacy replay notice acknowledgement", () => {
  test("persists acknowledgement and recognizes it on later executions", () => {
    const values = new Map<string, string>();
    const storage = {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
    };

    expect(hasAcknowledgedLegacyReplayNotice(storage)).toBe(false);
    acknowledgeLegacyReplayNotice(storage);
    expect(values.get(LEGACY_REPLAY_NOTICE_STORAGE_KEY)).toBe("true");
    expect(hasAcknowledgedLegacyReplayNotice(storage)).toBe(true);
  });

  test("does not break startup when browser storage is unavailable", () => {
    const storage = {
      getItem: () => {
        throw new Error("storage denied");
      },
      setItem: () => {
        throw new Error("storage denied");
      },
    };

    expect(hasAcknowledgedLegacyReplayNotice(storage)).toBe(false);
    expect(() => acknowledgeLegacyReplayNotice(storage)).not.toThrow();
  });
});
