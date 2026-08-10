import { type RefObject, useCallback, useEffect, useRef, useState } from "react";

import i18n from "../../i18n/i18n";
import type {
  ConnectionStatus,
  RenderCapabilities,
  RenderRequestOptions,
  RenderStatus,
} from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { localizedErrorMessage } from "../lib/localized-error";
import { type RenderPreviewLevels, RenderPreviewPlayer } from "../lib/render-preview-player";
import { useVisiblePolling } from "./use-visible-polling.hook";

const IDLE_STATUS: RenderStatus = {
  OperationId: null,
  RunId: null,
  State: "idle",
  ErrorCode: null,
  Message: null,
  FramesEncoded: 0,
  FramesCaptured: 0,
  EncodedSeconds: 0,
  OutputPath: null,
  Width: 0,
  Height: 0,
  VideoFps: 0,
  EncoderName: null,
  MicrophoneIncluded: false,
};

// A render can run for many minutes, so polling is slower than replay playback's 500 ms.
const POLL_INTERVAL_MS = 1000;

export function isRenderInProgress(status: RenderStatus) {
  return (
    status.State === "preparing" ||
    status.State === "opening_level" ||
    status.State === "capturing" ||
    status.State === "finalizing"
  );
}

export function useRenderControl(
  gatewayRef: RefObject<ActivityGateway | null>,
  connectionStatus: ConnectionStatus,
) {
  const [status, setStatus] = useState<RenderStatus>(IDLE_STATUS);
  const [capabilities, setCapabilities] = useState<RenderCapabilities | null>(null);
  const [pendingRunId, setPendingRunId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [errorRunId, setErrorRunId] = useState<string | null>(null);

  const statusRef = useRef(status);
  const pendingRunIdRef = useRef(pendingRunId);
  const startGenerationRef = useRef(0);
  const mountedRef = useRef(true);
  statusRef.current = status;
  pendingRunIdRef.current = pendingRunId;

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const refresh = useCallback(async () => {
    const gateway = gatewayRef.current;
    if (!gateway) return;
    try {
      const next = await gateway.getRenderStatus();
      if (!mountedRef.current) return;
      statusRef.current = next;
      setStatus(next);
    } catch {
      // A failed status poll is not worth surfacing: the next tick retries, and start/cancel
      // report their own failures.
    }
  }, [gatewayRef]);

  useEffect(() => {
    if (connectionStatus !== "online") return;
    const gateway = gatewayRef.current;
    if (!gateway) return;

    let cancelled = false;
    void (async () => {
      try {
        const next = await gateway.getRenderCapabilities();
        if (!cancelled && mountedRef.current) setCapabilities(next);
      } catch {
        if (!cancelled && mountedRef.current) setCapabilities(null);
      }
    })();
    void refresh();

    return () => {
      cancelled = true;
    };
  }, [connectionStatus, gatewayRef, refresh]);

  useVisiblePolling(() => {
    if (pendingRunIdRef.current || !isRenderInProgress(statusRef.current)) return;
    void refresh();
  }, POLL_INTERVAL_MS);

  const start = useCallback(
    async (runId: string, options?: RenderRequestOptions) => {
      const gateway = gatewayRef.current;
      if (!gateway) {
        setError(i18n.t("errors.notConnected", { ns: "common" }));
        setErrorRunId(runId);
        return false;
      }

      const generation = startGenerationRef.current + 1;
      startGenerationRef.current = generation;
      pendingRunIdRef.current = runId;
      setPendingRunId(runId);
      setError("");
      setErrorRunId(null);
      try {
        const next = await gateway.startRender(runId, options);
        if (generation !== startGenerationRef.current) return false;
        statusRef.current = next;
        setStatus(next);
        if (next.State === "error") {
          setError(next.Message ?? i18n.t("render.failed", { ns: "replay" }));
          setErrorRunId(runId);
          return false;
        }
        return true;
      } catch (cause) {
        if (generation === startGenerationRef.current) {
          setError(errorMessage(cause));
          setErrorRunId(runId);
        }
        return false;
      } finally {
        if (generation === startGenerationRef.current) {
          pendingRunIdRef.current = null;
          setPendingRunId(null);
        }
      }
    },
    [gatewayRef],
  );

  // The preview is a real (audio-only) render that returns raw per-source stems. They are cached
  // and mixed in the browser through gain nodes, so play/stop and the volume sliders work
  // instantly without re-rendering.
  const previewGenerationRef = useRef(0);
  const previewPlayerRef = useRef<RenderPreviewPlayer | null>(null);
  if (!previewPlayerRef.current) previewPlayerRef.current = new RenderPreviewPlayer();
  const previewPlayer = previewPlayerRef.current;
  const [previewState, setPreviewState] = useState<"idle" | "rendering" | "ready" | "playing">(
    "idle",
  );

  const renderPreview = useCallback(
    async (runId: string, options?: RenderRequestOptions) => {
      const gateway = gatewayRef.current;
      if (!gateway) return false;

      const generation = previewGenerationRef.current + 1;
      previewGenerationRef.current = generation;
      previewPlayer.clear();
      setPreviewState("rendering");
      setError("");
      setErrorRunId(null);

      const fail = (message?: string | null) => {
        if (generation === previewGenerationRef.current) {
          setPreviewState("idle");
          setError(message ?? i18n.t("render.preview.failed", { ns: "replay" }));
          setErrorRunId(runId);
        }
        return false;
      };

      try {
        let current = await gateway.startRenderPreview(runId, options);
        statusRef.current = current;
        setStatus(current);
        if (current.State === "error") return fail(current.Message);

        // Follow the preview render. Fast-forward plus ten seconds of mixing is usually well
        // under a minute; two minutes is the give-up point.
        const deadline = Date.now() + 120_000;
        while (Date.now() < deadline) {
          if (generation !== previewGenerationRef.current) return false;
          await delayMs(500);
          current = await gateway.getRenderStatus();
          statusRef.current = current;
          setStatus(current);
          if (current.State === "completed") break;
          if (current.State === "error" || current.State === "cancelled")
            return fail(current.Message);
        }
        if (current.State !== "completed") return fail();

        const stems = await gateway.getRenderPreviewResult();
        if (generation !== previewGenerationRef.current) return false;
        await previewPlayer.load(stems);
        if (generation !== previewGenerationRef.current) return false;
        setPreviewState("ready");
        return true;
      } catch (cause) {
        return fail(errorMessage(cause));
      }
    },
    [gatewayRef, previewPlayer],
  );

  const playPreview = useCallback(
    (levels: RenderPreviewLevels) => {
      const started = previewPlayer.play(levels, () => setPreviewState("ready"));
      if (started) setPreviewState("playing");
      return started;
    },
    [previewPlayer],
  );

  const stopPreviewPlayback = useCallback(() => {
    previewPlayer.stop();
    setPreviewState((state) => (state === "playing" ? "ready" : state));
  }, [previewPlayer]);

  /** Live gain update; audible immediately while the preview is playing. */
  const setPreviewLevels = useCallback((levels: RenderPreviewLevels) => {
    previewPlayerRef.current?.setLevels(levels);
  }, []);

  /** Live microphone timing trim; the mic source restarts at the corrected position. */
  const setPreviewMicTiming = useCallback((timingMs: number) => {
    previewPlayerRef.current?.setMicTiming(timingMs);
  }, []);

  const cancelPreviewRender = useCallback(async () => {
    previewGenerationRef.current += 1;
    previewPlayer.clear();
    setPreviewState("idle");
    const gateway = gatewayRef.current;
    if (!gateway) return false;
    try {
      await gateway.stopRenderPreview();
      void refresh();
      return true;
    } catch {
      // Stopping is best-effort: an already-finished preview needs no cancel.
      return false;
    }
  }, [gatewayRef, refresh, previewPlayer]);

  const cancel = useCallback(async () => {
    const gateway = gatewayRef.current;
    if (!gateway) return false;
    try {
      const next = await gateway.cancelRender();
      if (!mountedRef.current) return true;
      statusRef.current = next;
      setStatus(next);
      return true;
    } catch (cause) {
      if (mountedRef.current) {
        setError(errorMessage(cause));
        setErrorRunId(statusRef.current.RunId);
      }
      return false;
    }
  }, [gatewayRef]);

  return {
    status,
    capabilities,
    // The renderer is a separate mod with its own IPC namespace; a successful capabilities call is
    // what proves it is installed. Everything render-related in the UI hides until then.
    rendererDetected: capabilities !== null,
    pendingRunId,
    error,
    errorRunId,
    start,
    cancel,
    previewState,
    renderPreview,
    playPreview,
    stopPreviewPlayback,
    setPreviewLevels,
    setPreviewMicTiming,
    cancelPreviewRender,
    refresh,
    inProgress: isRenderInProgress(status),
  };
}

function delayMs(milliseconds: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, milliseconds));
}

function errorMessage(cause: unknown) {
  return localizedErrorMessage(cause, i18n.t("render.failed", { ns: "replay" }));
}
