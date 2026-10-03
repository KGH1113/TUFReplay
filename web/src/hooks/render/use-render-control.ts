import { useCallback, useEffect, useRef, useState } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import type { RenderApi } from "@/api/render/render-api";
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
import { renderOptionsSchema } from "@/schemas/render/render-schema";
import { ApiError } from "@/shared/errors/api-error";

type Phase =
  | "idle"
  | "checking"
  | "exporting"
  | "preparing"
  | "rendering"
  | "compositing"
  | "completed"
  | "failed"
  | "cancelled";
const pause = () => new Promise<void>((resolve) => setTimeout(resolve, 500));

export function useRenderControl() {
  const apiPromise = useApiPromise();
  const [run, setRun] = useState<ActivityRun | null>(null);
  const [options, setOptions] = useState<RenderOptions>(defaultRenderOptions);
  const [health, setHealth] = useState<RenderHealth | null>(null);
  const [settings, setSettings] = useState<RenderSettings | null>(null);
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
  const exportId = useRef<string | null>(null);
  const renderId = useRef<string | null>(null);
  const generation = useRef(0);
  const trigger = useRef<HTMLElement | null>(null);
  const directorySelectionId = useRef<string | null>(null);
  const directorySelectionApi = useRef<RenderApi | null>(null);
  const renderer = useCallback(async (): Promise<RenderApi> => {
    const api = (await apiPromise).render;
    if (!api)
      throw new ApiError("Install TUFReplay-Renderer and restart ADOFAI.", {
        kind: "domain",
        code: "renderer_missing",
      });
    return api;
  }, [apiPromise]);
  useEffect(() => {
    activeRef.current = true;
    return () => {
      activeRef.current = false;
      if (directorySelectionId.current && directorySelectionApi.current)
        void directorySelectionApi.current
          .cancelOutputDirectorySelection(directorySelectionId.current)
          .catch(() => {});
    };
  }, []);
  const fail = useCallback((cause: unknown) => {
    if (!activeRef.current) return;
    setPhase("failed");
    setErrorCode(cause instanceof ApiError ? cause.code : null);
    setErrorMessage(cause instanceof Error ? cause.message : "");
    setErrorDetails(null);
  }, []);
  const open = useCallback(
    (selected: ActivityRun, opener?: HTMLElement) => {
      if (busyRef.current) return;
      const current = ++generation.current;
      trigger.current =
        opener ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
      setRun(selected);
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
    const poll = async <T>(read: () => Promise<T>): Promise<T> => {
      while (activeRef.current) {
        try {
          const result = await read();
          setErrorCode(null);
          setErrorMessage("");
          return result;
        } catch (cause) {
          if (!(cause instanceof ApiError) || cause.kind !== "connection") throw cause;
          setErrorCode(cause.code);
          setErrorMessage(cause.message);
          await pause();
        }
      }
      throw new Error("Render status polling stopped.");
    };
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
      if (saveAsDefault) setSettings(await api.updateSettings(options));
      setPhase("exporting");
      let bundle = await api.exportBundle(run.id, options);
      exportId.current = bundle.jobId;
      while (bundle.state === "preparing") {
        if (cancelledRef.current) {
          try {
            await api.cancelExport(bundle.jobId);
          } catch (cause) {
            if (!(cause instanceof ApiError) || cause.kind !== "connection") throw cause;
          }
        }
        if (!activeRef.current) return;
        setProgress(bundle.progress);
        await pause();
        bundle = await poll(() => api.getExportStatus(bundle.jobId));
      }
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
        ...options,
        includeDmNote: options.includeDmNote && ready.dmNoteConfigured,
      });
      renderId.current = rendered.jobId;
      while (!renderJobFinished(rendered)) {
        if (cancelledRef.current) {
          try {
            await api.cancel(rendered.jobId);
          } catch (cause) {
            if (!(cause instanceof ApiError) || cause.kind !== "connection") throw cause;
          }
        }
        if (!activeRef.current) return;
        setJob(rendered);
        setPhase(rendered.state);
        setProgress(rendered.progress);
        await pause();
        rendered = await poll(() => api.getStatus(rendered.jobId));
      }
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
      fail(cause);
      if (
        cause instanceof ApiError &&
        cause.cause &&
        typeof cause.cause === "object" &&
        "details" in cause.cause
      )
        setErrorDetails((cause.cause as { details: RenderExportStatus["errorDetails"] }).details);
    } finally {
      busyRef.current = false;
    }
  }, [run, options, renderer, fail, saveAsDefault]);
  const cancel = useCallback(async () => {
    cancelledRef.current = true;
    try {
      const api = await renderer();
      if (renderId.current) await api.cancel(renderId.current);
      else if (exportId.current) await api.cancelExport(exportId.current);
    } catch (cause) {
      setErrorCode(cause instanceof ApiError ? cause.code : null);
      setErrorMessage(cause instanceof Error ? cause.message : "");
    }
  }, [renderer]);
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
    setChoosingDirectory(true);
    setErrorCode(null);
    setErrorMessage("");
    try {
      const api = await renderer();
      directorySelectionApi.current = api;
      let chosen = await api.chooseOutputDirectory(options.outputDirectory);
      directorySelectionId.current = chosen.selectionId;
      while (chosen.pending && activeRef.current && current === generation.current) {
        await pause();
        if (!activeRef.current || current !== generation.current) break;
        chosen = await api.getOutputDirectorySelection(chosen.selectionId);
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
      if (current !== generation.current) return;
      setErrorCode(cause instanceof ApiError ? cause.code : null);
      setErrorMessage(cause instanceof Error ? cause.message : "");
    } finally {
      directorySelectionId.current = null;
      directorySelectionApi.current = null;
      if (activeRef.current) setChoosingDirectory(false);
    }
  }, [options.outputDirectory, renderer, choosingDirectory]);
  const busy =
    (phase === "checking" && busyRef.current) ||
    phase === "exporting" ||
    phase === "preparing" ||
    phase === "rendering" ||
    phase === "compositing";
  return {
    run,
    options,
    setOptions,
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
