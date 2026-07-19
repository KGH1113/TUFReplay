import { clampMicrophoneOffset } from "./microphone-offset.utils";
import { clampMicrophoneVolumeDb, DEFAULT_MICROPHONE_VOLUME_DB } from "./microphone-volume.utils";

export type MicrophoneOffsetCalibrationPhase =
  | "closed"
  | "launching"
  | "waiting_for_clear"
  | "editing"
  | "error";

export interface MicrophoneOffsetCalibrationState {
  phase: MicrophoneOffsetCalibrationPhase;
  offsetMs: number;
  microphoneVolumeDb: number;
}

export type MicrophoneOffsetCalibrationAction =
  | { type: "start" }
  | { type: "level_opened" }
  | { type: "run_cleared" }
  | { type: "commit_offset"; offsetMs: number }
  | { type: "commit_microphone_volume"; volumeDb: number }
  | {
      type: "sync";
      phase: MicrophoneOffsetCalibrationPhase;
      offsetMs: number;
      microphoneVolumeDb: number;
    }
  | { type: "close" };

export function createMicrophoneOffsetCalibrationState(
  initialOffsetMs: number,
  initialMicrophoneVolumeDb = DEFAULT_MICROPHONE_VOLUME_DB,
): MicrophoneOffsetCalibrationState {
  return {
    phase: "closed",
    offsetMs: clampMicrophoneOffset(initialOffsetMs),
    microphoneVolumeDb: clampMicrophoneVolumeDb(initialMicrophoneVolumeDb),
  };
}

export function microphoneOffsetCalibrationReducer(
  state: MicrophoneOffsetCalibrationState,
  action: MicrophoneOffsetCalibrationAction,
): MicrophoneOffsetCalibrationState {
  if (action.type === "start") return { ...state, phase: "launching" };
  if (action.type === "level_opened" && state.phase === "launching")
    return { ...state, phase: "waiting_for_clear" };
  if (action.type === "run_cleared" && state.phase === "waiting_for_clear")
    return { ...state, phase: "editing" };
  if (action.type === "commit_offset")
    return { ...state, offsetMs: clampMicrophoneOffset(action.offsetMs) };
  if (action.type === "commit_microphone_volume")
    return {
      ...state,
      microphoneVolumeDb: clampMicrophoneVolumeDb(action.volumeDb),
    };
  if (action.type === "sync")
    return {
      phase: action.phase,
      offsetMs: clampMicrophoneOffset(action.offsetMs),
      microphoneVolumeDb: clampMicrophoneVolumeDb(action.microphoneVolumeDb),
    };
  if (action.type === "close") return { ...state, phase: "closed" };
  return state;
}
