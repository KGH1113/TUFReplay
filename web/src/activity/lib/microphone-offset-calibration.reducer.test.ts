import { describe, expect, test } from "bun:test";

import {
  createMicrophoneOffsetCalibrationState,
  microphoneOffsetCalibrationReducer,
} from "./microphone-offset-calibration.reducer";

describe("microphone offset calibration reducer", () => {
  test("moves through the mock run and preserves committed offset after closing", () => {
    let state = createMicrophoneOffsetCalibrationState(0);
    state = microphoneOffsetCalibrationReducer(state, { type: "start" });
    expect(state.phase).toBe("launching");
    state = microphoneOffsetCalibrationReducer(state, { type: "level_opened" });
    expect(state.phase).toBe("waiting_for_clear");
    state = microphoneOffsetCalibrationReducer(state, { type: "run_cleared" });
    expect(state.phase).toBe("editing");
    state = microphoneOffsetCalibrationReducer(state, { type: "commit_offset", offsetMs: 96 });
    state = microphoneOffsetCalibrationReducer(state, { type: "close" });
    expect(state).toEqual({ phase: "closed", offsetMs: 96 });
  });

  test("ignores out-of-order automatic transitions", () => {
    const state = createMicrophoneOffsetCalibrationState(0);
    expect(microphoneOffsetCalibrationReducer(state, { type: "run_cleared" })).toBe(state);
  });
});
