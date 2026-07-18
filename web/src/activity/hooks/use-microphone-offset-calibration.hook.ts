import { useCallback, useEffect, useReducer, useRef, useState } from "react";

import { clampMicrophoneOffset } from "../lib/microphone-offset.utils";
import {
  createMicrophoneOffsetCalibrationState,
  microphoneOffsetCalibrationReducer,
} from "../lib/microphone-offset-calibration.reducer";
import { MockMicrophoneOffsetAudioPlayer } from "../lib/mock-microphone-offset-audio";
import { mockMicrophoneOffsetCalibration } from "../mock/microphone-offset.mock";

const LEVEL_LAUNCH_DELAY_MS = 650;
const MOCK_CLEAR_DELAY_MS = 1_350;

export function useMicrophoneOffsetCalibration() {
  const [state, dispatch] = useReducer(
    microphoneOffsetCalibrationReducer,
    mockMicrophoneOffsetCalibration.initialOffsetMs,
    createMicrophoneOffsetCalibrationState,
  );
  const [playing, setPlaying] = useState(false);
  const [playbackPositionMs, setPlaybackPositionMs] = useState(0);
  const [audioError, setAudioError] = useState("");
  const playerRef = useRef<MockMicrophoneOffsetAudioPlayer | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const playbackStartedAtRef = useRef(0);
  const playingRef = useRef(false);

  const stopPlayback = useCallback((positionMs = 0) => {
    playingRef.current = false;
    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    playerRef.current?.stop();
    setPlaying(false);
    setPlaybackPositionMs(positionMs);
  }, []);

  const start = useCallback(() => {
    stopPlayback();
    setAudioError("");
    dispatch({ type: "start" });
  }, [stopPlayback]);

  const close = useCallback(() => {
    stopPlayback();
    setAudioError("");
    dispatch({ type: "close" });
  }, [stopPlayback]);

  const commitOffset = useCallback((offsetMs: number) => {
    const nextOffsetMs = clampMicrophoneOffset(offsetMs);
    dispatch({ type: "commit_offset", offsetMs: nextOffsetMs });
    if (playingRef.current) playerRef.current?.updateOffset(nextOffsetMs);
  }, []);

  const togglePlayback = useCallback(async () => {
    if (playingRef.current) {
      stopPlayback();
      return;
    }

    setAudioError("");
    setPlaybackPositionMs(0);
    setPlaying(true);
    playingRef.current = true;
    playbackStartedAtRef.current = performance.now();

    const animate = (now: number) => {
      if (!playingRef.current) return;
      const positionMs = Math.min(
        mockMicrophoneOffsetCalibration.durationMs,
        now - playbackStartedAtRef.current,
      );
      setPlaybackPositionMs(positionMs);
      if (positionMs >= mockMicrophoneOffsetCalibration.durationMs) {
        stopPlayback(mockMicrophoneOffsetCalibration.durationMs);
        return;
      }
      animationFrameRef.current = requestAnimationFrame(animate);
    };
    animationFrameRef.current = requestAnimationFrame(animate);

    try {
      const player =
        playerRef.current ?? new MockMicrophoneOffsetAudioPlayer(mockMicrophoneOffsetCalibration);
      playerRef.current = player;
      await player.play(state.offsetMs);
    } catch (cause) {
      playerRef.current?.dispose();
      playerRef.current = null;
      setAudioError(
        cause instanceof Error ? cause.message : "Could not start the mock audio preview.",
      );
    }
  }, [state.offsetMs, stopPlayback]);

  useEffect(() => {
    if (state.phase === "launching") {
      const timeout = window.setTimeout(
        () => dispatch({ type: "level_opened" }),
        LEVEL_LAUNCH_DELAY_MS,
      );
      return () => window.clearTimeout(timeout);
    }
    if (state.phase === "waiting_for_clear") {
      const timeout = window.setTimeout(
        () => dispatch({ type: "run_cleared" }),
        MOCK_CLEAR_DELAY_MS,
      );
      return () => window.clearTimeout(timeout);
    }
    return undefined;
  }, [state.phase]);

  useEffect(
    () => () => {
      if (animationFrameRef.current !== null) cancelAnimationFrame(animationFrameRef.current);
      playerRef.current?.dispose();
    },
    [],
  );

  return {
    data: mockMicrophoneOffsetCalibration,
    phase: state.phase,
    offsetMs: state.offsetMs,
    playing,
    playbackPositionMs,
    audioError,
    start,
    close,
    commitOffset,
    resetOffset: () => commitOffset(0),
    togglePlayback,
  };
}
