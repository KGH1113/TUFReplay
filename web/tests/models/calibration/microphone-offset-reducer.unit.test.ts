import { describe, expect, test } from "bun:test";

import {
  createMicrophoneOffsetCalibrationState,
  microphoneOffsetCalibrationReducer,
} from "@/state/calibration/microphone-offset-reducer";

describe("microphone offset calibration reducer", () => {
  test("opens timing settings without starting calibration", () => {
    let state = createMicrophoneOffsetCalibrationState(0);
    state = microphoneOffsetCalibrationReducer(state, {
      type: "open_settings",
      offsetMs: 84,
      microphoneVolumeDb: 4,
    });
    expect(state).toEqual({ phase: "settings", offsetMs: 84, microphoneVolumeDb: 4 });
    state = microphoneOffsetCalibrationReducer(state, { type: "start" });
    expect(state.phase).toBe("launching");
  });

  test("moves through the mock run and preserves committed offset after closing", () => {
    let state = createMicrophoneOffsetCalibrationState(0);
    state = microphoneOffsetCalibrationReducer(state, { type: "start" });
    expect(state.phase).toBe("launching");
    state = microphoneOffsetCalibrationReducer(state, { type: "level_opened" });
    expect(state.phase).toBe("waiting_for_clear");
    state = microphoneOffsetCalibrationReducer(state, { type: "run_cleared" });
    expect(state.phase).toBe("editing");
    state = microphoneOffsetCalibrationReducer(state, { type: "commit_offset", offsetMs: 96 });
    state = microphoneOffsetCalibrationReducer(state, {
      type: "commit_microphone_volume",
      volumeDb: 12,
    });
    state = microphoneOffsetCalibrationReducer(state, { type: "close" });
    expect(state).toEqual({
      phase: "closed",
      offsetMs: 96,
      microphoneVolumeDb: 12,
    });
  });

  test("clamps microphone volume to the preview range", () => {
    let state = createMicrophoneOffsetCalibrationState(0, 40);
    expect(state.microphoneVolumeDb).toBe(30);
    state = microphoneOffsetCalibrationReducer(state, {
      type: "commit_microphone_volume",
      volumeDb: -30,
    });
    expect(state.microphoneVolumeDb).toBe(-20);
  });

  test("ignores out-of-order automatic transitions", () => {
    const state = createMicrophoneOffsetCalibrationState(0);
    expect(microphoneOffsetCalibrationReducer(state, { type: "run_cleared" })).toBe(state);
  });

  test("preserves state identity when a polled sync contains no changes", () => {
    const state = createMicrophoneOffsetCalibrationState(42, 3);
    expect(
      microphoneOffsetCalibrationReducer(state, {
        type: "sync",
        phase: "closed",
        offsetMs: 42,
        microphoneVolumeDb: 3,
      }),
    ).toBe(state);
  });
});
