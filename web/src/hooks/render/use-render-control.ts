import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import type { RenderApi } from "@/api/render/render-api";
import { waitForAppState } from "@/application/wait-for-app-state";
import { downloadsQueryKey } from "@/hooks/downloads/use-downloads";
import type { ActivityRun } from "@/models/activity/activity-model";
import {
  defaultRenderOptions,
  type RenderExportStatus,
  type RenderHealth,
  type RenderJob,
  type RenderOptions,
  type RenderSettings,
  renderJobFinished,
} from "@/models/render/render-model";
import {
  applyRenderRecommendation,
  createRenderRecommendations,
  type RenderQuality,
  type RenderSettingsMode,
} from "@/models/render/render-recommendations";
import { renderOptionsSchema } from "@/schemas/render/render-schema";
import { ApiError } from "@/shared/errors/api-error";

type Phase =
  | "idle"
  | "checking"
  | "installing"
  | "exporting"
  | "preparing"
  | "rendering"
  | "compositing"
  | "completed"
  | "failed"
  | "cancelled";

export function useRenderControl() {
  const apiPromise = useApiPromise();
  const queryClient = useQueryClient();
  const [run, setRun] = useState<ActivityRun | null>(null);
  const [levelChoiceRun, setLevelChoiceRun] = useState<ActivityRun | null>(null);
  const [levelPath, setLevelPath] = useState<string | undefined>();
  const [customOptions, setOptions] = useState<RenderOptions>(defaultRenderOptions);
  const [health, setHealth] = useState<RenderHealth | null>(null);
  const [settings, setSettings] = useState<RenderSettings | null>(null);
  const [settingsMode, setSettingsMode] = useState<RenderSettingsMode>("recommended");
  const [quality, setQuality] = useState<RenderQuality | null>(null);
  const recommendations = useMemo(() => createRenderRecommendations(settings), [settings]);
  const options = useMemo(
    () =>
      settingsMode === "recommended"
        ? applyRenderRecommendation(customOptions, recommendations, quality)
        : customOptions,
    [customOptions, quality, recommendations, settingsMode],
  );
  const [saveAsDefault, setSaveAsDefault] = useState(false);
  const [choosingDirectory, setChoosingDirectory] = useState(false);
  const [openingDirectory, setOpeningDirectory] = useState(false);
  const [phase, setPhase] = useState<Phase>("idle");
  const [progress, setProgress] = useState(0);
  const [job, setJob] = useState<RenderJob | null>(null);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState("");
  const [errorDetails, setErrorDetails] = useState<RenderExportStatus["errorDetails"]>(null);
  const activeRef = useRef(true);
  const busyRef = useRef(false);
  const cancelledRef = useRef(false);
  const actionAbort = useRef<AbortController | null>(null);
  const directoryAbort = useRef<AbortController | null>(null);
  const exportId = useRef<string | null>(null);
  const renderId = useRef<string | null>(null);
  const waitingForInstall = useRef(false);
  const generation = useRef(0);
  const trigger = useRef<HTMLElement | null>(null);
  const directorySelectionId = useRef<string | null>(null);
  const directorySelectionApi = useRef<RenderApi | null>(null);
  const renderer = useCallback(async (): Promise<RenderApi> => {
    const app = await apiPromise;
    if (app.downloads) {
      const state = await app.downloads.getStatus();
      queryClient.setQueryData(downloadsQueryKey, state);
      if (state.Renderer.Status !== "ready")
        throw new ApiError("Install or enable TUFReplay-Renderer and restart ADOFAI.", {
          kind: "domain",
          code: "renderer_missing",
        });
    }
    const api = app.render;
    if (!api)
      throw new ApiError("Install TUFReplay-Renderer and restart ADOFAI.", {
        kind: "domain",
        code: "renderer_missing",
      });
    return api;
  }, [apiPromise, queryClient]);
  useEffect(() => {
    activeRef.current = true;
    return () => {
      activeRef.current = false;
      actionAbort.current?.abort();
      directoryAbort.current?.abort();
      const activeRender = renderId.current;
      const activeExport = exportId.current;
      if (activeRender || activeExport)
        void apiPromise
          .then(async (app) => {
            if (activeRender) await app.render?.cancel(activeRender);
            else if (activeExport) await app.render?.cancelExport(activeExport);
          })
          .catch(() => {});
      if (waitingForInstall.current)
        void apiPromise.then((api) => api.downloads?.cancelPendingFfmpeg()).catch(() => {});
      if (directorySelectionId.current && directorySelectionApi.current)
        void directorySelectionApi.current
          .cancelOutputDirectorySelection(directorySelectionId.current)
          .catch(() => {});
    };
  }, [apiPromise]);
  const fail = useCallback((cause: unknown) => {
    if (!activeRef.current) return;
    setPhase("failed");
    setErrorCode(cause instanceof ApiError ? cause.code : null);
    setErrorMessage(cause instanceof Error ? cause.message : "");
    setErrorDetails(null);
  }, []);
  useEffect(() => {
    let disposed = false;
    const cleanup: Array<() => void> = [];
    void apiPromise.then((app) => {
      if (disposed || !app.events) return;
      cleanup.push(
        app.events.on("renderer.settings.changed", (next) => {
          if (activeRef.current) setSettings(next);
        }),
      );
      cleanup.push(
        app.events.on("renderer.health.changed", (next) => {
          if (activeRef.current) setHealth(next);
        }),
      );
    });
    return () => {
      disposed = true;
      for (const off of cleanup) off();
    };
  }, [apiPromise]);
  const open = useCallback(
    (selected: ActivityRun, opener?: HTMLElement, selectedLevelPath?: string) => {
      if (busyRef.current) return;
      const current = ++generation.current;
      trigger.current =
        opener ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
      setRun(selected);
      setLevelChoiceRun(null);
      setLevelPath(selectedLevelPath);
      setHealth(null);
      setSettings(null);
      setJob(null);
      setPhase("checking");
      setProgress(0);
      setErrorCode(null);
      setErrorMessage("");
      setErrorDetails(null);
      void renderer()
        .then(async (api) => Promise.all([api.getHealth(), api.getSettings()]))
        .then(([state, saved]) => {
          if (!activeRef.current || current !== generation.current) return;
          setHealth(state);
          setSettings(saved);
          setSettingsMode(saved.preferences?.mode ?? "recommended");
          setQuality(saved.preferences?.quality ?? null);
          setOptions({
            ...saved.defaults,
            outputDirectory: saved.defaults.outputDirectory || saved.outputDirectory,
          });
          setPhase("idle");
        })
        .catch((cause) => {
          if (current === generation.current) fail(cause);
        });
    },
    [renderer, fail],
  );
  const openLevelChoice = useCallback((selected: ActivityRun, opener?: HTMLElement) => {
    if (busyRef.current) return;
    trigger.current =
      opener ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
    setRun(null);
    setSettingsMode("recommended");
    setQuality(null);
    setLevelChoiceRun(selected);
  }, []);
  const chooseLevel = useCallback(
    async (runId: string, selectedLevelPath?: string) => {
      if (!levelChoiceRun || levelChoiceRun.id !== runId || busyRef.current) return false;
      open(levelChoiceRun, trigger.current ?? undefined, selectedLevelPath);
      return true;
    },
    [levelChoiceRun, open],
  );
  const start = useCallback(async () => {
    if (!run || busyRef.current) return;
    busyRef.current = true;
    cancelledRef.current = false;
    exportId.current = null;
    renderId.current = null;
    setPhase("checking");
    setProgress(0);
    setErrorCode(null);
    setErrorMessage("");
    setErrorDetails(null);
    let chosenOptions = options;
    const abort = new AbortController();
    actionAbort.current = abort;
    try {
      const api = await renderer();
      const ready = await api.getHealth();
      setHealth(ready);
      if (!ready.available || !ready.orbitAvailable)
        throw new ApiError("OrbitRender is unavailable.", {
          kind: "domain",
          code: "orbit_missing",
        });
      if (ready.busy)
        throw new ApiError("Another render is running.", { kind: "domain", code: "render_busy" });
      if (cancelledRef.current) {
        setPhase("cancelled");
        return;
      }
      const app = await apiPromise;
      if (!app.events) throw new Error("Render state subscriptions are unavailable.");
      const events = app.events;
      const downloads = app.downloads;
      if (downloads) {
        let state = await downloads.getStatus();
        if (state.Ffmpeg.Status !== "ready") {
          waitingForInstall.current = true;
          setPhase("installing");
          state = await downloads.act("ffmpeg", "request");
          queryClient.setQueryData(downloadsQueryKey, state);
          if (!["ready", "failed", "cancelled", "declined"].includes(state.Ffmpeg.Status))
            state = await waitForAppState(
              events,
              "downloads.changed",
              (next) => ["ready", "failed", "cancelled", "declined"].includes(next.Ffmpeg.Status),
              {
                namespace: "recorder",
                signal: abort.signal,
                onState: (next) => queryClient.setQueryData(downloadsQueryKey, next),
              },
            );
          if (["cancelled", "declined"].includes(state.Ffmpeg.Status))
            throw new ApiError(
              "FFmpeg installation was skipped. Start rendering again to install it.",
              { kind: "domain", code: "ffmpeg_install_declined" },
            );
          if (state.Ffmpeg.Status === "failed")
            throw new ApiError(state.Ffmpeg.Error ?? "FFmpeg installation failed.", {
              kind: "domain",
              code: "ffmpeg_install_failed",
            });
          waitingForInstall.current = false;
          await downloads.cancelPendingFfmpeg();
          queryClient.setQueryData(downloadsQueryKey, state);
          if (!activeRef.current) return;
          if (cancelledRef.current) {
            setPhase("cancelled");
            return;
          }
        }
      }
      if (settingsMode === "recommended") {
        // First-use installation may have changed the usable encoders. Resolve
        // the selected quality once, then use that exact profile for the job.
        let latest = await api.getSettings();
        if (latest.system?.encoding.state === "checking")
          latest = await waitForAppState(
            events,
            "renderer.settings.changed",
            (next) => next.system?.encoding.state !== "checking",
            {
              namespace: "renderer",
              signal: abort.signal,
              timeoutMs: 20_000,
              onState: setSettings,
            },
          );
        if (!activeRef.current) return;
        if (cancelledRef.current) {
          setPhase("cancelled");
          return;
        }
        setSettings(latest);
        const resolved = createRenderRecommendations(latest);
        const selected = resolved.levels.find(
          (value) => value.quality === (quality ?? resolved.recommended),
        );
        if (!selected?.supported)
          throw new ApiError(
            "This GPU cannot render the selected resolution. Choose a lower quality.",
            {
              kind: "domain",
              code: "render_resolution_unsupported",
            },
          );
        chosenOptions = applyRenderRecommendation(customOptions, resolved, quality);
      }
      if (saveAsDefault)
        setSettings(await api.updateSettings(chosenOptions, { mode: settingsMode, quality }));
      setPhase("exporting");
      let bundle = await api.exportBundle(run.id, chosenOptions, levelPath);
      exportId.current = bundle.jobId;
      if (abort.signal.aborted) {
        void api.cancelExport(bundle.jobId).catch(() => {});
        throw abort.signal.reason;
      }
      if (bundle.state === "preparing")
        bundle = await waitForAppState(
          events,
          "render-bundle.changed",
          (next) => next.jobId === bundle.jobId && next.state !== "preparing",
          {
            namespace: "recorder",
            signal: abort.signal,
            onState: (next) => {
              if (next.jobId === exportId.current && activeRef.current) setProgress(next.progress);
            },
          },
        );
      if (bundle.state === "cancelled" || cancelledRef.current) {
        setPhase("cancelled");
        return;
      }
      if (bundle.state !== "completed" || !bundle.manifestPath) {
        setErrorDetails(bundle.errorDetails);
        throw new ApiError(bundle.errorMessage ?? "Export failed.", {
          kind: "domain",
          code: bundle.errorCode ?? "render_export_failed",
          cause: { details: bundle.errorDetails },
        });
      }
      setPhase("preparing");
      setProgress(0);
      let rendered = await api.start(bundle.manifestPath, {
        ...chosenOptions,
        includeDmNote: chosenOptions.includeDmNote && ready.dmNoteConfigured,
      });
      renderId.current = rendered.jobId;
      if (abort.signal.aborted) {
        void api.cancel(rendered.jobId).catch(() => {});
        throw abort.signal.reason;
      }
      if (!renderJobFinished(rendered))
        rendered = await waitForAppState(
          events,
          "renderer.job.changed",
          (next) => next.jobId === rendered.jobId && renderJobFinished(next),
          {
            namespace: "renderer",
            signal: abort.signal,
            onState: (next) => {
              if (next.jobId !== renderId.current || !activeRef.current) return;
              setJob(next);
              setPhase(next.state);
              setProgress(next.progress);
            },
          },
        );
      if (!activeRef.current) return;
      setJob(rendered);
      setProgress(rendered.progress);
      setPhase(rendered.state);
      if (rendered.state === "failed") {
        setErrorCode(rendered.errorCode);
        setErrorMessage(rendered.errorMessage ?? "");
        setErrorDetails(rendered.errorDetails);
      }
    } catch (cause) {
      if (abort.signal.aborted || cancelledRef.current) {
        if (activeRef.current) setPhase("cancelled");
        return;
      }
      fail(cause);
      if (
        cause instanceof ApiError &&
        cause.cause &&
        typeof cause.cause === "object" &&
        "details" in cause.cause
      )
        setErrorDetails((cause.cause as { details: RenderExportStatus["errorDetails"] }).details);
    } finally {
      if (waitingForInstall.current) {
        waitingForInstall.current = false;
        void apiPromise.then((api) => api.downloads?.cancelPendingFfmpeg()).catch(() => {});
      }
      if (actionAbort.current === abort) actionAbort.current = null;
      busyRef.current = false;
    }
  }, [
    run,
    options,
    customOptions,
    quality,
    settingsMode,
    levelPath,
    renderer,
    fail,
    saveAsDefault,
    apiPromise,
    queryClient,
  ]);
  const cancel = useCallback(async () => {
    cancelledRef.current = true;
    actionAbort.current?.abort();
    try {
      if (waitingForInstall.current) {
        await (await apiPromise).downloads?.cancelPendingFfmpeg();
        return;
      }
      const api = await renderer();
      if (renderId.current) await api.cancel(renderId.current);
      else if (exportId.current) await api.cancelExport(exportId.current);
    } catch (cause) {
      setErrorCode(cause instanceof ApiError ? cause.code : null);
      setErrorMessage(cause instanceof Error ? cause.message : "");
    }
  }, [renderer, apiPromise]);
  const openOutputDirectory = useCallback(async () => {
    if (!job?.canOpenOutput || openingDirectory) return;
    setOpeningDirectory(true);
    try {
      if (!(await (await renderer()).openOutputDirectory(job.jobId)).opened)
        throw new ApiError("The save folder could not be opened.", {
          kind: "domain",
          code: "output_directory_open_failed",
        });
    } catch (cause) {
      setErrorCode(cause instanceof ApiError ? cause.code : null);
      setErrorMessage(cause instanceof Error ? cause.message : "");
    } finally {
      setOpeningDirectory(false);
    }
  }, [job, renderer, openingDirectory]);
  const chooseOutputDirectory = useCallback(async () => {
    if (busyRef.current || choosingDirectory) return;
    const current = generation.current;
    const abort = new AbortController();
    directoryAbort.current = abort;
    setChoosingDirectory(true);
    setErrorCode(null);
    setErrorMessage("");
    try {
      const api = await renderer();
      directorySelectionApi.current = api;
      let chosen = await api.chooseOutputDirectory(options.outputDirectory);
      directorySelectionId.current = chosen.selectionId;
      if (abort.signal.aborted) {
        void api.cancelOutputDirectorySelection(chosen.selectionId).catch(() => {});
        return;
      }
      if (chosen.pending) {
        const app = await apiPromise;
        if (!app.events) throw new Error("Folder selection subscriptions are unavailable.");
        chosen = await waitForAppState(
          app.events,
          "renderer.folder.changed",
          (next) => next.selectionId === chosen.selectionId && !next.pending,
          { namespace: "renderer", signal: abort.signal },
        );
      }
      if (!activeRef.current || current !== generation.current) {
        await api.cancelOutputDirectorySelection(chosen.selectionId);
        return;
      }
      if (chosen.errorCode)
        throw new ApiError(chosen.errorMessage ?? "The save folder could not be selected.", {
          kind: "domain",
          code: chosen.errorCode,
        });
      if (chosen.outputDirectory !== null) {
        const outputDirectory = chosen.outputDirectory;
        setOptions((previous) => ({ ...previous, outputDirectory }));
      }
    } catch (cause) {
      if (directorySelectionId.current && directorySelectionApi.current)
        void directorySelectionApi.current
          .cancelOutputDirectorySelection(directorySelectionId.current)
          .catch(() => {});
      if (abort.signal.aborted || current !== generation.current) return;
      setErrorCode(cause instanceof ApiError ? cause.code : null);
      setErrorMessage(cause instanceof Error ? cause.message : "");
    } finally {
      if (directoryAbort.current === abort) directoryAbort.current = null;
      directorySelectionId.current = null;
      directorySelectionApi.current = null;
      if (activeRef.current) setChoosingDirectory(false);
    }
  }, [options.outputDirectory, renderer, choosingDirectory, apiPromise]);
  const busy =
    (phase === "checking" && busyRef.current) ||
    phase === "installing" ||
    phase === "exporting" ||
    phase === "preparing" ||
    phase === "rendering" ||
    phase === "compositing";
  return {
    run,
    levelChoiceRun,
    levelPath,
    openLevelChoice,
    chooseLevel,
    closeLevelChoice: () => setLevelChoiceRun(null),
    options,
    setOptions,
    settingsMode,
    setSettingsMode: (mode: RenderSettingsMode) => {
      if (mode === "advanced") setOptions(options);
      setSettingsMode(mode);
    },
    quality: quality ?? recommendations.recommended,
    selectQuality: setQuality,
    recommendations,
    health,
    settings,
    saveAsDefault,
    setSaveAsDefault,
    choosingDirectory,
    openingDirectory,
    chooseOutputDirectory,
    openOutputDirectory,
    phase,
    progress,
    job,
    errorCode,
    errorMessage,
    errorDetails,
    optionValidation: renderOptionsSchema.safeParse(options),
    busy,
    open,
    close: () => {
      if (!busyRef.current) {
        ++generation.current;
        directoryAbort.current?.abort();
        if (directorySelectionId.current && directorySelectionApi.current)
          void directorySelectionApi.current
            .cancelOutputDirectorySelection(directorySelectionId.current)
            .catch(() => {});
        setRun(null);
        requestAnimationFrame(() => {
          if (trigger.current?.isConnected) trigger.current.focus();
        });
      }
    },
    start,
    cancel,
  };
}
