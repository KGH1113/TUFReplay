import { clampMicrophoneOffset } from "@/models/calibration/microphone-offset";

export type MicrophoneOffsetCalibrationPhase =
  | "closed"
  | "settings"
  | "launching"
  | "waiting_for_clear"
  | "editing"
  | "error";

export interface MicrophoneOffsetCalibrationState {
  phase: MicrophoneOffsetCalibrationPhase;
  offsetMs: number;
}

export type MicrophoneOffsetCalibrationAction =
  | { type: "open_settings"; offsetMs: number }
  | { type: "start" }
  | { type: "level_opened" }
  | { type: "run_cleared" }
  | { type: "commit_offset"; offsetMs: number }
  | {
      type: "sync";
      phase: MicrophoneOffsetCalibrationPhase;
      offsetMs: number;
    }
  | { type: "close" };

export function createMicrophoneOffsetCalibrationState(
  initialOffsetMs: number,
): MicrophoneOffsetCalibrationState {
  return {
    phase: "closed",
    offsetMs: clampMicrophoneOffset(initialOffsetMs),
  };
}

export function microphoneOffsetCalibrationReducer(
  state: MicrophoneOffsetCalibrationState,
  action: MicrophoneOffsetCalibrationAction,
): MicrophoneOffsetCalibrationState {
  if (action.type === "open_settings")
    return {
      phase: "settings",
      offsetMs: clampMicrophoneOffset(action.offsetMs),
    };
  if (action.type === "start") return { ...state, phase: "launching" };
  if (action.type === "level_opened" && state.phase === "launching")
    return { ...state, phase: "waiting_for_clear" };
  if (action.type === "run_cleared" && state.phase === "waiting_for_clear")
    return { ...state, phase: "editing" };
  if (action.type === "commit_offset")
    return { ...state, offsetMs: clampMicrophoneOffset(action.offsetMs) };
  if (action.type === "sync") {
    const offsetMs = clampMicrophoneOffset(action.offsetMs);
    if (state.phase === action.phase && state.offsetMs === offsetMs) return state;
    return { phase: action.phase, offsetMs };
  }
  if (action.type === "close") return { ...state, phase: "closed" };
  return state;
}
