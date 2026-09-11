import { describe, expect, test } from "bun:test";

import type { ActivityRun } from "@/models/activity/activity-model";
import { replayButtonState } from "@/models/activity/replay-button-state";

const run = (replayPlayable: boolean): ActivityRun => ({ replayPlayable }) as ActivityRun;

describe("replay button state", () => {
  test("keeps a playable replay interactive", () => {
    expect(replayButtonState(run(true), false, false)).toEqual({
      permanentlyUnavailable: false,
      disabled: false,
      ariaDisabled: false,
      tooltipTabIndex: undefined,
      cursor: "pointer",
    });
  });

  test("makes permanent replay failures native-disabled and keyboard-tooltip reachable", () => {
    expect(replayButtonState(run(false), false, false)).toEqual({
      permanentlyUnavailable: true,
      disabled: true,
      ariaDisabled: true,
      tooltipTabIndex: 0,
      cursor: "not-allowed",
    });
  });

  test("distinguishes read-only and in-progress states from permanent failures", () => {
    expect(replayButtonState(run(true), true, false)).toMatchObject({
      permanentlyUnavailable: false,
      disabled: true,
      ariaDisabled: true,
      cursor: "default",
    });
    expect(replayButtonState(run(true), false, true)).toMatchObject({
      permanentlyUnavailable: false,
      disabled: false,
      ariaDisabled: true,
      cursor: "wait",
    });
  });
});
