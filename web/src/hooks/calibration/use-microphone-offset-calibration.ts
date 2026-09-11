import type { RefObject } from "react";
import { useCallback, useEffect, useReducer, useRef, useState } from "react";
import type {
  CalibrationGateway,
  LegacyCalibrationResult as MicrophoneCalibrationResult,
  LegacyCalibrationStatus as MicrophoneCalibrationStatus,
  LegacyMicrophoneTimingSettings as MicrophoneTimingSettings,
} from "@/hooks/calibration/use-calibration-gateway-adapter";
import i18n from "@/i18n/i18n";
import { mockMicrophoneOffsetCalibration } from "@/mocks/calibration/microphone-offset-fixture";
import { MockMicrophoneOffsetAudioPlayer } from "@/mocks/calibration/mock-microphone-offset-audio";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import type { CalibrationState } from "@/models/calibration/calibration-model";
import { clampMicrophoneOffset } from "@/models/calibration/microphone-offset";
import type { MicrophoneOffsetCalibrationData } from "@/models/calibration/microphone-offset-calibration-data";
import { clampMicrophoneVolumeDb } from "@/models/calibration/microphone-volume";
import {
  createMicrophoneOffsetCalibrationState,
  type MicrophoneOffsetCalibrationPhase,
  microphoneOffsetCalibrationReducer,
} from "@/state/calibration/microphone-offset-reducer";

const LEVEL_LAUNCH_DELAY_MS = 650;
const MOCK_CLEAR_DELAY_MS = 1_350;
const STATUS_POLL_INTERVAL_MS = 100;
const VOLUME_UPDATE_INTERVAL_MS = 50;
const TIMING_SETTINGS_SAVE_DELAY_MS = 120;

type CalibrationPollTimer = ReturnType<typeof setTimeout>;

interface CalibrationStatusPollingOptions {
  getGateway: () => CalibrationGateway | null;
  getOperationId: () => string | null;
  onStatus: (status: MicrophoneCalibrationStatus, operationId: string) => Promise<void> | void;
  onError: (cause: unknown) => void;
  intervalMs?: number;
  isVisible?: () => boolean;
  schedule?: (callback: () => void, delayMs: number) => CalibrationPollTimer;
  cancelSchedule?: (timer: CalibrationPollTimer) => void;
}

export function createCalibrationOffsetSaveQueue(onError: (cause: unknown) => void) {
  let tail = Promise.resolve();

  return {
    enqueue(save: () => Promise<unknown>) {
      tail = tail.then(save).then(
        () => undefined,
        (cause) => onError(cause),
      );
    },
    flush() {
      return tail;
    },
  };
}

export interface CalibrationOffsetCommit {
  id: number;
  offsetMs: number;
}

export function createCalibrationOffsetReconciler(initialOffsetMs: number) {
  let confirmedOffsetMs = clampMicrophoneOffset(initialOffsetMs);
  let pendingCommit: CalibrationOffsetCommit | null = null;
  let pendingCommitResolved = false;
  let nextCommitId = 0;

  const currentOffset = () => pendingCommit?.offsetMs ?? confirmedOffsetMs;

  return {
    begin(offsetMs: number): CalibrationOffsetCommit {
      const commit = {
        id: ++nextCommitId,
        offsetMs: clampMicrophoneOffset(offsetMs),
      };
      pendingCommit = commit;
      pendingCommitResolved = false;
      return commit;
    },
    synchronize(statusOffsetMs: number) {
      const clampedStatusOffsetMs = clampMicrophoneOffset(statusOffsetMs);
      if (pendingCommit === null) confirmedOffsetMs = clampedStatusOffsetMs;
      else if (pendingCommitResolved && pendingCommit.offsetMs === clampedStatusOffsetMs) {
        confirmedOffsetMs = clampedStatusOffsetMs;
        pendingCommit = null;
        pendingCommitResolved = false;
      }
      return currentOffset();
    },
    resolve(commit: CalibrationOffsetCommit, statusOffsetMs: number) {
      confirmedOffsetMs = clampMicrophoneOffset(statusOffsetMs);
      const latest = pendingCommit?.id === commit.id;
      if (latest) pendingCommitResolved = true;
      return { offsetMs: currentOffset(), latest };
    },
    reject(commit: CalibrationOffsetCommit) {
      const latest = pendingCommit?.id === commit.id;
      if (latest) {
        pendingCommit = null;
        pendingCommitResolved = false;
      }
      return { offsetMs: currentOffset(), latest };
    },
  };
}

export function installCalibrationStatusPolling({
  getGateway,
  getOperationId,
  onStatus,
  onError,
  intervalMs = STATUS_POLL_INTERVAL_MS,
  isVisible = () => typeof document === "undefined" || document.visibilityState === "visible",
  schedule = (callback, delayMs) => setTimeout(callback, delayMs),
  cancelSchedule = (timer) => clearTimeout(timer),
}: CalibrationStatusPollingOptions) {
  let cancelled = false;
  let pollingActive = true;
  let timer: CalibrationPollTimer | null = null;

  const poll = async () => {
    if (cancelled) return;
    if (!isVisible()) {
      timer = schedule(() => void poll(), intervalMs);
      return;
    }
    const gateway = getGateway();
    const operationId = getOperationId();
    try {
      if (gateway && operationId) {
        const status = await gateway.getMicrophoneCalibrationStatus(operationId);
        if (!cancelled && status.OperationId === operationId && getOperationId() === operationId) {
          await onStatus(status, operationId);
          pollingActive = isCalibrationPollingState(status.State);
        }
      }
    } catch (cause) {
      if (!cancelled) onError(cause);
    } finally {
      if (!cancelled && pollingActive) timer = schedule(() => void poll(), intervalMs);
    }
  };

  timer = schedule(() => void poll(), intervalMs);
  return () => {
    cancelled = true;
    if (timer !== null) cancelSchedule(timer);
  };
}

export function isCalibrationPollingState(state: MicrophoneCalibrationStatus["State"]) {
  return state !== "idle" && state !== "editing" && state !== "error";
}

export function useMicrophoneOffsetCalibration(
  gatewayRef: RefObject<CalibrationGateway | null>,
  connectionStatus: ConnectionStatus,
  mockEnabled: boolean,
  initialOffsetMs: number,
  initialMicrophoneVolumeDb: number,
  onTimingSettingsChange: (settings: MicrophoneTimingSettings) => void,
) {
  const [state, dispatch] = useReducer(microphoneOffsetCalibrationReducer, undefined, () =>
    createMicrophoneOffsetCalibrationState(
      mockEnabled ? mockMicrophoneOffsetCalibration.initialOffsetMs : initialOffsetMs,
      initialMicrophoneVolumeDb,
    ),
  );
  const [data, setData] = useState<MicrophoneOffsetCalibrationData>(
    mockMicrophoneOffsetCalibration,
  );
  const [playing, setPlaying] = useState(false);
  const [playbackPositionMs, setPlaybackPositionMs] = useState(0);
  const [audioError, setAudioError] = useState("");
  const playerRef = useRef<MockMicrophoneOffsetAudioPlayer | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const playbackPositionRef = useRef(0);
  const playbackStartedAtRef = useRef(0);
  const playingRef = useRef(false);
  const operationIdRef = useRef<string | null>(null);
  const activeGatewayRef = useRef<CalibrationGateway | null>(null);
  const backendStateRef = useRef<CalibrationState>("idle");
  const resultRevisionRef = useRef(0);
  const positionAnchorRef = useRef({ positionMs: 0, sampledAtMs: 0 });
  const durationRef = useRef(mockMicrophoneOffsetCalibration.durationMs);
  const offsetSaveQueueRef = useRef(
    createCalibrationOffsetSaveQueue((cause) =>
      setAudioError(errorMessage(cause, i18n.t("errors.saveOffset", { ns: "microphone" }))),
    ),
  );
  const offsetReconcilerRef = useRef(createCalibrationOffsetReconciler(state.offsetMs));
  const pendingVolumeRef = useRef<number | null>(null);
  const volumeTimerRef = useRef<number | null>(null);
  const requestGenerationRef = useRef(0);
  const phaseRef = useRef<MicrophoneOffsetCalibrationPhase>(state.phase);
  const pendingTimingOffsetRef = useRef<number | null>(null);
  const pendingTimingVolumeRef = useRef<number | null>(null);
  const timingSaveTimerRef = useRef<number | null>(null);
  const timingSaveTailRef = useRef(Promise.resolve());
  const timingDraftRef = useRef({
    MicrophoneOffsetMs: state.offsetMs,
    MicrophoneVolumeDb: state.microphoneVolumeDb,
  });
  phaseRef.current = state.phase;
  timingDraftRef.current = {
    MicrophoneOffsetMs: state.offsetMs,
    MicrophoneVolumeDb: state.microphoneVolumeDb,
  };

  const stopLocalPlayback = useCallback((positionMs = 0) => {
    playingRef.current = false;
    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    playerRef.current?.stop();
    playbackPositionRef.current = positionMs;
    setPlaying(false);
    setPlaybackPositionMs(positionMs);
  }, []);

  const flushTimingSettings = useCallback(async () => {
    if (timingSaveTimerRef.current !== null) {
      window.clearTimeout(timingSaveTimerRef.current);
      timingSaveTimerRef.current = null;
    }
    const offsetMs = pendingTimingOffsetRef.current;
    const volumeDb = pendingTimingVolumeRef.current;
    pendingTimingOffsetRef.current = null;
    pendingTimingVolumeRef.current = null;
    if (offsetMs === null && volumeDb === null) {
      await timingSaveTailRef.current;
      return;
    }
    if (mockEnabled) {
      onTimingSettingsChange(timingDraftRef.current);
      return;
    }

    const gateway = gatewayRef.current;
    if (!gateway || connectionStatus !== "online") {
      if (offsetMs !== null) pendingTimingOffsetRef.current = offsetMs;
      if (volumeDb !== null) pendingTimingVolumeRef.current = volumeDb;
      setAudioError(i18n.t("errors.notConnectedToGame", { ns: "common" }));
      throw new Error("Microphone timing settings are unavailable while disconnected.");
    }

    const save = timingSaveTailRef.current.then(async () => {
      let settings: MicrophoneTimingSettings | null = null;
      if (offsetMs !== null) settings = await gateway.setMicrophoneOffset(offsetMs);
      if (volumeDb !== null) settings = await gateway.setMicrophoneVolume(volumeDb);
      if (settings) onTimingSettingsChange(settings);
    });
    timingSaveTailRef.current = save.catch(() => undefined);
    try {
      await save;
      setAudioError("");
    } catch (cause) {
      if (offsetMs !== null && pendingTimingOffsetRef.current === null)
        pendingTimingOffsetRef.current = offsetMs;
      if (volumeDb !== null && pendingTimingVolumeRef.current === null)
        pendingTimingVolumeRef.current = volumeDb;
      setAudioError(errorMessage(cause, i18n.t("errors.saveTiming", { ns: "microphone" })));
      throw cause;
    }
  }, [connectionStatus, gatewayRef, mockEnabled, onTimingSettingsChange]);

  const scheduleTimingSettingsSave = useCallback(() => {
    if (timingSaveTimerRef.current !== null) window.clearTimeout(timingSaveTimerRef.current);
    timingSaveTimerRef.current = window.setTimeout(() => {
      timingSaveTimerRef.current = null;
      void flushTimingSettings().catch(() => undefined);
    }, TIMING_SETTINGS_SAVE_DELAY_MS);
  }, [flushTimingSettings]);

  const openSettings = useCallback(() => {
    stopLocalPlayback();
    setAudioError("");
    offsetReconcilerRef.current = createCalibrationOffsetReconciler(initialOffsetMs);
    dispatch({
      type: "open_settings",
      offsetMs: initialOffsetMs,
      microphoneVolumeDb: initialMicrophoneVolumeDb,
    });
  }, [initialMicrophoneVolumeDb, initialOffsetMs, stopLocalPlayback]);

  const applyBackendResult = useCallback((result: MicrophoneCalibrationResult) => {
    resultRevisionRef.current = result.Revision;
    durationRef.current = Math.max(1, result.DurationMs);
    setData({
      durationMs: durationRef.current,
      songWaveform: result.SongWaveform ?? result.GameWaveform,
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
      offsetMs: offsetReconcilerRef.current.synchronize(status.MicrophoneOffsetMs),
      microphoneVolumeDb: status.MicrophoneVolumeDb,
    });

    const previewActive = status.State === "preview_starting" || status.State === "preview_playing";
    const playingChanged = playingRef.current !== previewActive;
    playingRef.current = previewActive;
    if (playingChanged) setPlaying(previewActive);
    positionAnchorRef.current = {
      positionMs: status.PlaybackPositionMs,
      sampledAtMs: performance.now(),
    };
    playbackPositionRef.current = status.PlaybackPositionMs;
    if (!previewActive) setPlaybackPositionMs(status.PlaybackPositionMs);
    setAudioError(
      status.State === "error"
        ? status.Message || i18n.t("errors.calibrationFailed", { ns: "microphone" })
        : "",
    );
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
        setAudioError(errorMessage(cause, i18n.t("errors.loadWaveforms", { ns: "microphone" })));
      }
    },
    [applyBackendResult, gatewayRef],
  );

  const start = useCallback(async () => {
    if (phaseRef.current === "settings") {
      try {
        await flushTimingSettings();
      } catch {
        return;
      }
    }
    const generation = ++requestGenerationRef.current;
    stopLocalPlayback();
    setAudioError("");
    playbackPositionRef.current = 0;
    setPlaybackPositionMs(0);
    resultRevisionRef.current = 0;
    dispatch({ type: "start" });
    if (mockEnabled) return;
    durationRef.current = 1;
    setData({
      durationMs: 1,
      songWaveform: new Array(2048).fill(0),
      microphoneWaveform: new Array(2048).fill(0),
    });

    const gateway = gatewayRef.current;
    if (!gateway || connectionStatus !== "online") {
      setAudioError(i18n.t("errors.notConnectedToGame", { ns: "common" }));
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
      setAudioError(errorMessage(cause, i18n.t("errors.startCalibration", { ns: "microphone" })));
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
    flushTimingSettings,
    gatewayRef,
    loadBackendResult,
    mockEnabled,
    state.microphoneVolumeDb,
    state.offsetMs,
    stopLocalPlayback,
  ]);

  const close = useCallback(async () => {
    if (phaseRef.current === "settings") {
      try {
        await flushTimingSettings();
      } catch {
        return;
      }
    }
    requestGenerationRef.current += 1;
    const gateway = gatewayRef.current ?? activeGatewayRef.current;
    const operationId = operationIdRef.current;
    const pendingOffsetSaves = offsetSaveQueueRef.current.flush();
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
        await pendingOffsetSaves;
        if (pendingVolume !== null)
          await gateway.setMicrophoneCalibrationVolume(operationId, pendingVolume);
        await gateway.closeMicrophoneCalibration(operationId);
      })().catch(() => undefined);
  }, [flushTimingSettings, gatewayRef, mockEnabled, stopLocalPlayback]);

  const commitOffset = useCallback(
    (offsetMs: number) => {
      const nextOffsetMs = clampMicrophoneOffset(offsetMs);
      dispatch({ type: "commit_offset", offsetMs: nextOffsetMs });
      if (phaseRef.current === "settings") {
        pendingTimingOffsetRef.current = nextOffsetMs;
        scheduleTimingSettingsSave();
        return;
      }
      if (mockEnabled) {
        offsetReconcilerRef.current = createCalibrationOffsetReconciler(nextOffsetMs);
        if (playingRef.current) playerRef.current?.updateOffset(nextOffsetMs);
        return;
      }
      const gateway = gatewayRef.current;
      const operationId = operationIdRef.current;
      if (gateway && operationId) {
        const reconciler = offsetReconcilerRef.current;
        const commit = reconciler.begin(nextOffsetMs);
        offsetSaveQueueRef.current.enqueue(async () => {
          try {
            const status = await gateway.setMicrophoneCalibrationOffset(operationId, nextOffsetMs);
            const resolved = reconciler.resolve(commit, status.MicrophoneOffsetMs);
            if (!resolved.latest) return status;
            onTimingSettingsChange({
              MicrophoneOffsetMs: status.MicrophoneOffsetMs,
              MicrophoneVolumeDb: status.MicrophoneVolumeDb,
            });
            return status;
          } catch (cause) {
            const rejected = reconciler.reject(commit);
            if (!rejected.latest) return undefined;
            if (operationIdRef.current === operationId)
              dispatch({ type: "commit_offset", offsetMs: rejected.offsetMs });
            throw cause;
          }
        });
      }
    },
    [gatewayRef, mockEnabled, onTimingSettingsChange, scheduleTimingSettingsSave],
  );

  const commitMicrophoneVolume = useCallback(
    (volumeDb: number) => {
      const nextVolumeDb = clampMicrophoneVolumeDb(volumeDb);
      dispatch({ type: "commit_microphone_volume", volumeDb: nextVolumeDb });
      if (phaseRef.current === "settings") {
        pendingTimingVolumeRef.current = nextVolumeDb;
        scheduleTimingSettingsSave();
        return;
      }
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
          .then((status) =>
            onTimingSettingsChange({
              MicrophoneOffsetMs: status.MicrophoneOffsetMs,
              MicrophoneVolumeDb: status.MicrophoneVolumeDb,
            }),
          )
          .catch((cause) =>
            setAudioError(errorMessage(cause, i18n.t("errors.saveVolume", { ns: "microphone" }))),
          );
      }, VOLUME_UPDATE_INTERVAL_MS);
    },
    [gatewayRef, mockEnabled, onTimingSettingsChange, scheduleTimingSettingsSave],
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
        setAudioError(errorMessage(cause, i18n.t("errors.controlPreview", { ns: "microphone" })));
      }
      return;
    }

    if (playingRef.current) {
      stopLocalPlayback();
      return;
    }
    setAudioError("");
    playbackPositionRef.current = 0;
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
      playbackPositionRef.current = positionMs;
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
      setAudioError(errorMessage(cause, i18n.t("errors.startMockPreview", { ns: "microphone" })));
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
    if (
      mockEnabled ||
      state.phase === "closed" ||
      state.phase === "settings" ||
      state.phase === "error"
    )
      return undefined;
    return installCalibrationStatusPolling({
      getGateway: () => gatewayRef.current ?? activeGatewayRef.current,
      getOperationId: () => operationIdRef.current,
      onStatus: async (status, operationId) => {
        await loadBackendResult(status);
        if (operationIdRef.current !== operationId) return;
        applyBackendStatus(status);
      },
      onError: (cause) =>
        setAudioError(errorMessage(cause, i18n.t("errors.refreshStatus", { ns: "microphone" }))),
    });
  }, [applyBackendStatus, gatewayRef, loadBackendResult, mockEnabled, state.phase]);

  const getPlaybackPositionMs = useCallback(() => {
    if (!mockEnabled && playingRef.current && backendStateRef.current === "preview_playing")
      return extrapolateCalibrationPlaybackPosition(
        positionAnchorRef.current,
        performance.now(),
        durationRef.current,
      );
    return playbackPositionRef.current;
  }, [mockEnabled]);

  useEffect(
    () => () => {
      if (animationFrameRef.current !== null) cancelAnimationFrame(animationFrameRef.current);
      if (volumeTimerRef.current !== null) window.clearTimeout(volumeTimerRef.current);
      if (timingSaveTimerRef.current !== null) window.clearTimeout(timingSaveTimerRef.current);
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
    getPlaybackPositionMs,
    audioError,
    openSettings,
    start,
    close,
    commitOffset,
    commitMicrophoneVolume,
    resetOffset: () => commitOffset(0),
    togglePlayback,
  };
}

function phaseFromBackendState(state: CalibrationState): MicrophoneOffsetCalibrationPhase {
  if (state === "idle") return "closed";
  if (state === "error") return "error";
  if (state === "arming" || state === "opening_level") return "launching";
  if (state === "waiting_for_run" || state === "recording" || state === "processing")
    return "waiting_for_clear";
  return "editing";
}

export function extrapolateCalibrationPlaybackPosition(
  anchor: { positionMs: number; sampledAtMs: number },
  nowMs: number,
  durationMs: number,
) {
  if (anchor.positionMs <= 0) return 0;
  return Math.min(durationMs, anchor.positionMs + Math.max(0, nowMs - anchor.sampledAtMs));
}

function errorMessage(cause: unknown, fallback: string) {
  return localizedErrorMessage(cause, fallback);
}
