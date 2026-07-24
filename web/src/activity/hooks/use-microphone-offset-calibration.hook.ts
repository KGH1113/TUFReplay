import type { RefObject } from "react";
import { useCallback, useEffect, useReducer, useRef, useState } from "react";

import type {
  ConnectionStatus,
  MicrophoneCalibrationResult,
  MicrophoneCalibrationState,
  MicrophoneCalibrationStatus,
  MicrophoneOffsetCalibrationData,
} from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { clampMicrophoneOffset } from "../lib/microphone-offset.utils";
import {
  createMicrophoneOffsetCalibrationState,
  type MicrophoneOffsetCalibrationPhase,
  microphoneOffsetCalibrationReducer,
} from "../lib/microphone-offset-calibration.reducer";
import { clampMicrophoneVolumeDb } from "../lib/microphone-volume.utils";
import { MockMicrophoneOffsetAudioPlayer } from "../lib/mock-microphone-offset-audio";
import { mockMicrophoneOffsetCalibration } from "../mock/microphone-offset.mock";

const LEVEL_LAUNCH_DELAY_MS = 650;
const MOCK_CLEAR_DELAY_MS = 1_350;
const STATUS_POLL_INTERVAL_MS = 100;
const VOLUME_UPDATE_INTERVAL_MS = 50;

type CalibrationPollTimer = ReturnType<typeof setTimeout>;

interface CalibrationStatusPollingOptions {
  getGateway: () => ActivityGateway | null;
  getOperationId: () => string | null;
  onStatus: (status: MicrophoneCalibrationStatus, operationId: string) => Promise<void> | void;
  onError: (cause: unknown) => void;
  intervalMs?: number;
  schedule?: (callback: () => void, delayMs: number) => CalibrationPollTimer;
  cancelSchedule?: (timer: CalibrationPollTimer) => void;
}

export function installCalibrationStatusPolling({
  getGateway,
  getOperationId,
  onStatus,
  onError,
  intervalMs = STATUS_POLL_INTERVAL_MS,
  schedule = (callback, delayMs) => setTimeout(callback, delayMs),
  cancelSchedule = (timer) => clearTimeout(timer),
}: CalibrationStatusPollingOptions) {
  let cancelled = false;
  let timer: CalibrationPollTimer | null = null;

  const poll = async () => {
    if (cancelled) return;
    const gateway = getGateway();
    const operationId = getOperationId();
    try {
      if (gateway && operationId) {
        const status = await gateway.getMicrophoneCalibrationStatus(operationId);
        if (!cancelled && status.OperationId === operationId && getOperationId() === operationId)
          await onStatus(status, operationId);
      }
    } catch (cause) {
      if (!cancelled) onError(cause);
    } finally {
      if (!cancelled) timer = schedule(() => void poll(), intervalMs);
    }
  };

  timer = schedule(() => void poll(), intervalMs);
  return () => {
    cancelled = true;
    if (timer !== null) cancelSchedule(timer);
  };
}

export function useMicrophoneOffsetCalibration(
  gatewayRef: RefObject<ActivityGateway | null>,
  connectionStatus: ConnectionStatus,
  mockEnabled: boolean,
) {
  const [state, dispatch] = useReducer(
    microphoneOffsetCalibrationReducer,
    mockMicrophoneOffsetCalibration.initialOffsetMs,
    createMicrophoneOffsetCalibrationState,
  );
  const [data, setData] = useState<MicrophoneOffsetCalibrationData>(
    mockMicrophoneOffsetCalibration,
  );
  const [playing, setPlaying] = useState(false);
  const [playbackPositionMs, setPlaybackPositionMs] = useState(0);
  const [audioError, setAudioError] = useState("");
  const playerRef = useRef<MockMicrophoneOffsetAudioPlayer | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const playbackStartedAtRef = useRef(0);
  const playingRef = useRef(false);
  const operationIdRef = useRef<string | null>(null);
  const activeGatewayRef = useRef<ActivityGateway | null>(null);
  const backendStateRef = useRef<MicrophoneCalibrationState>("idle");
  const resultRevisionRef = useRef(0);
  const positionAnchorRef = useRef({ positionMs: 0, sampledAtMs: 0 });
  const durationRef = useRef(mockMicrophoneOffsetCalibration.durationMs);
  const pendingVolumeRef = useRef<number | null>(null);
  const volumeTimerRef = useRef<number | null>(null);
  const requestGenerationRef = useRef(0);

  const stopLocalPlayback = useCallback((positionMs = 0) => {
    playingRef.current = false;
    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    playerRef.current?.stop();
    setPlaying(false);
    setPlaybackPositionMs(positionMs);
  }, []);

  const applyBackendResult = useCallback((result: MicrophoneCalibrationResult) => {
    resultRevisionRef.current = result.Revision;
    durationRef.current = Math.max(1, result.DurationMs);
    setData({
      durationMs: durationRef.current,
      gameWaveform: result.GameWaveform,
      microphoneWaveform: result.MicrophoneWaveform,
    });
  }, []);

  const applyBackendStatus = useCallback((status: MicrophoneCalibrationStatus) => {
    if (status.OperationId) operationIdRef.current = status.OperationId;
    backendStateRef.current = status.State;
    if (status.DurationMs > 0) durationRef.current = status.DurationMs;

    const phase = phaseFromBackendState(status.State);
    dispatch({
      type: "sync",
      phase,
      offsetMs: status.MicrophoneOffsetMs,
      microphoneVolumeDb: status.MicrophoneVolumeDb,
    });

    const previewActive = status.State === "preview_starting" || status.State === "preview_playing";
    playingRef.current = previewActive;
    setPlaying(previewActive);
    positionAnchorRef.current = {
      positionMs: status.PlaybackPositionMs,
      sampledAtMs: performance.now(),
    };
    setPlaybackPositionMs(status.PlaybackPositionMs);
    setAudioError(status.State === "error" ? status.Message || "Calibration failed." : "");
  }, []);

  const loadBackendResult = useCallback(
    async (status: MicrophoneCalibrationStatus) => {
      const gateway = gatewayRef.current ?? activeGatewayRef.current;
      const operationId = operationIdRef.current;
      if (
        !gateway ||
        !operationId ||
        status.ResultRevision <= 0 ||
        status.ResultRevision <= resultRevisionRef.current
      )
        return;

      const revision = status.ResultRevision;
      resultRevisionRef.current = revision;
      try {
        applyBackendResult(await gateway.getMicrophoneCalibrationResult(operationId, revision));
      } catch (cause) {
        resultRevisionRef.current = 0;
        setAudioError(errorMessage(cause, "Could not load the calibration waveforms."));
      }
    },
    [applyBackendResult, gatewayRef],
  );

  const start = useCallback(async () => {
    const generation = ++requestGenerationRef.current;
    stopLocalPlayback();
    setAudioError("");
    setPlaybackPositionMs(0);
    resultRevisionRef.current = 0;
    dispatch({ type: "start" });
    if (mockEnabled) return;
    durationRef.current = 1;
    setData({
      durationMs: 1,
      gameWaveform: new Array(480).fill(0),
      microphoneWaveform: new Array(480).fill(0),
    });

    const gateway = gatewayRef.current;
    if (!gateway || connectionStatus !== "online") {
      setAudioError("TUFReplay is not connected to ADOFAI.");
      dispatch({
        type: "sync",
        phase: "error",
        offsetMs: state.offsetMs,
        microphoneVolumeDb: state.microphoneVolumeDb,
      });
      return;
    }
    activeGatewayRef.current = gateway;
    try {
      const status = await gateway.startMicrophoneCalibration();
      if (generation !== requestGenerationRef.current) {
        if (status.OperationId)
          void gateway.closeMicrophoneCalibration(status.OperationId).catch(() => undefined);
        return;
      }
      applyBackendStatus(status);
      await loadBackendResult(status);
    } catch (cause) {
      setAudioError(errorMessage(cause, "Could not start microphone calibration."));
      dispatch({
        type: "sync",
        phase: "error",
        offsetMs: state.offsetMs,
        microphoneVolumeDb: state.microphoneVolumeDb,
      });
    }
  }, [
    applyBackendStatus,
    connectionStatus,
    gatewayRef,
    loadBackendResult,
    mockEnabled,
    state.microphoneVolumeDb,
    state.offsetMs,
    stopLocalPlayback,
  ]);

  const close = useCallback(() => {
    requestGenerationRef.current += 1;
    const gateway = gatewayRef.current ?? activeGatewayRef.current;
    const operationId = operationIdRef.current;
    const pendingVolume = pendingVolumeRef.current;
    pendingVolumeRef.current = null;
    if (volumeTimerRef.current !== null) {
      window.clearTimeout(volumeTimerRef.current);
      volumeTimerRef.current = null;
    }
    stopLocalPlayback();
    setAudioError("");
    operationIdRef.current = null;
    activeGatewayRef.current = null;
    backendStateRef.current = "idle";
    resultRevisionRef.current = 0;
    dispatch({ type: "close" });
    if (!mockEnabled && gateway && operationId)
      void (async () => {
        if (pendingVolume !== null)
          await gateway.setMicrophoneCalibrationVolume(operationId, pendingVolume);
        await gateway.closeMicrophoneCalibration(operationId);
      })().catch(() => undefined);
  }, [gatewayRef, mockEnabled, stopLocalPlayback]);

  const commitOffset = useCallback(
    (offsetMs: number) => {
      const nextOffsetMs = clampMicrophoneOffset(offsetMs);
      dispatch({ type: "commit_offset", offsetMs: nextOffsetMs });
      if (mockEnabled) {
        if (playingRef.current) playerRef.current?.updateOffset(nextOffsetMs);
        return;
      }
      const gateway = gatewayRef.current;
      const operationId = operationIdRef.current;
      if (gateway && operationId)
        void gateway
          .setMicrophoneCalibrationOffset(operationId, nextOffsetMs)
          .catch((cause) => setAudioError(errorMessage(cause, "Could not save the offset.")));
    },
    [gatewayRef, mockEnabled],
  );

  const commitMicrophoneVolume = useCallback(
    (volumeDb: number) => {
      const nextVolumeDb = clampMicrophoneVolumeDb(volumeDb);
      dispatch({ type: "commit_microphone_volume", volumeDb: nextVolumeDb });
      if (mockEnabled) {
        playerRef.current?.updateMicrophoneVolume(nextVolumeDb);
        return;
      }
      pendingVolumeRef.current = nextVolumeDb;
      if (volumeTimerRef.current !== null) return;
      volumeTimerRef.current = window.setTimeout(() => {
        volumeTimerRef.current = null;
        const gateway = gatewayRef.current;
        const operationId = operationIdRef.current;
        const pendingVolume = pendingVolumeRef.current;
        pendingVolumeRef.current = null;
        if (!gateway || !operationId || pendingVolume === null) return;
        void gateway
          .setMicrophoneCalibrationVolume(operationId, pendingVolume)
          .catch((cause) => setAudioError(errorMessage(cause, "Could not save the volume.")));
      }, VOLUME_UPDATE_INTERVAL_MS);
    },
    [gatewayRef, mockEnabled],
  );

  const togglePlayback = useCallback(async () => {
    if (!mockEnabled) {
      const gateway = gatewayRef.current;
      const operationId = operationIdRef.current;
      if (!gateway || !operationId) return;
      setAudioError("");
      try {
        const status = playingRef.current
          ? await gateway.stopMicrophoneCalibrationPreview(operationId)
          : await gateway.playMicrophoneCalibrationPreview(operationId);
        if (operationIdRef.current !== operationId) return;
        applyBackendStatus(status);
      } catch (cause) {
        setAudioError(errorMessage(cause, "Could not control the calibration preview."));
      }
      return;
    }

    if (playingRef.current) {
      stopLocalPlayback();
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
        stopLocalPlayback(mockMicrophoneOffsetCalibration.durationMs);
        return;
      }
      animationFrameRef.current = requestAnimationFrame(animate);
    };
    animationFrameRef.current = requestAnimationFrame(animate);

    try {
      const player =
        playerRef.current ?? new MockMicrophoneOffsetAudioPlayer(mockMicrophoneOffsetCalibration);
      playerRef.current = player;
      await player.play(state.offsetMs, state.microphoneVolumeDb);
    } catch (cause) {
      playerRef.current?.dispose();
      playerRef.current = null;
      stopLocalPlayback();
      setAudioError(errorMessage(cause, "Could not start the mock audio preview."));
    }
  }, [
    applyBackendStatus,
    gatewayRef,
    mockEnabled,
    state.microphoneVolumeDb,
    state.offsetMs,
    stopLocalPlayback,
  ]);

  useEffect(() => {
    if (!mockEnabled) return undefined;
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
  }, [mockEnabled, state.phase]);

  useEffect(() => {
    if (mockEnabled || state.phase === "closed" || state.phase === "error") return undefined;
    return installCalibrationStatusPolling({
      getGateway: () => gatewayRef.current ?? activeGatewayRef.current,
      getOperationId: () => operationIdRef.current,
      onStatus: async (status, operationId) => {
        await loadBackendResult(status);
        if (operationIdRef.current !== operationId) return;
        applyBackendStatus(status);
      },
      onError: (cause) =>
        setAudioError(errorMessage(cause, "Could not refresh calibration status.")),
    });
  }, [applyBackendStatus, gatewayRef, loadBackendResult, mockEnabled, state.phase]);

  useEffect(() => {
    if (mockEnabled || state.phase === "closed") return undefined;
    let frame = 0;
    const animate = (now: number) => {
      if (playingRef.current && backendStateRef.current === "preview_playing") {
        const anchor = positionAnchorRef.current;
        setPlaybackPositionMs(
          Math.min(durationRef.current, anchor.positionMs + Math.max(0, now - anchor.sampledAtMs)),
        );
      }
      frame = requestAnimationFrame(animate);
    };
    frame = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(frame);
  }, [mockEnabled, state.phase]);

  useEffect(
    () => () => {
      if (animationFrameRef.current !== null) cancelAnimationFrame(animationFrameRef.current);
      if (volumeTimerRef.current !== null) window.clearTimeout(volumeTimerRef.current);
      playerRef.current?.dispose();
    },
    [],
  );

  useEffect(
    () => () => {
      requestGenerationRef.current += 1;
      const operationId = operationIdRef.current;
      const gateway = gatewayRef.current ?? activeGatewayRef.current;
      if (!mockEnabled && operationId && gateway)
        void gateway.closeMicrophoneCalibration(operationId).catch(() => undefined);
    },
    [gatewayRef, mockEnabled],
  );

  return {
    data,
    phase: state.phase,
    offsetMs: state.offsetMs,
    microphoneVolumeDb: state.microphoneVolumeDb,
    playing,
    playbackPositionMs,
    audioError,
    start,
    close,
    commitOffset,
    commitMicrophoneVolume,
    resetOffset: () => commitOffset(0),
    togglePlayback,
  };
}

function phaseFromBackendState(
  state: MicrophoneCalibrationState,
): MicrophoneOffsetCalibrationPhase {
  if (state === "idle") return "closed";
  if (state === "error") return "error";
  if (state === "arming" || state === "opening_level") return "launching";
  if (state === "waiting_for_run" || state === "recording" || state === "processing")
    return "waiting_for_clear";
  return "editing";
}

function errorMessage(cause: unknown, fallback: string) {
  return cause instanceof Error ? cause.message : fallback;
}
