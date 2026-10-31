import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import type { RenderApi } from "@/api/render/render-api";
import { readLastRenderOptions, saveLastRenderOptions } from "@/hooks/render/render-option-storage";
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

const pollIntervalMs = 500;
const pause = () => new Promise<void>((resolve) => setTimeout(resolve, pollIntervalMs));

export function useRenderControl() {
  const apiPromise = useApiPromise();
  const [run, setRun] = useState<ActivityRun | null>(null);
  const [levelPath, setLevelPath] = useState<string | undefined>();
  const [options, setOptions] = useState<RenderOptions>(defaultRenderOptions);
  const [health, setHealth] = useState<RenderHealth | null>(null);
  const [settings, setSettings] = useState<RenderSettings | null>(null);
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
  const optionValidation = useMemo(() => renderOptionsSchema.safeParse(options), [options]);

  const renderer = useCallback(async (): Promise<RenderApi> => {
    const app = await apiPromise;
    if (!app.render)
      throw new ApiError("Install and enable OrbitRender, then restart ADOFAI.", {
        kind: "domain",
        code: "orbit_missing",
      });
    return app.render;
  }, [apiPromise]);

  useEffect(() => {
    activeRef.current = true;
    return () => {
      activeRef.current = false;
      const activeRender = renderId.current;
      const activeExport = exportId.current;
      if (activeRender || activeExport)
        void apiPromise
          .then(async (app) => {
            if (activeRender) await app.render?.cancel(activeRender);
            else if (activeExport) await app.render?.cancelExport(activeExport);
          })
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

  const open = useCallback(
    (selected: ActivityRun, opener?: HTMLElement, selectedLevelPath?: string) => {
      if (busyRef.current) return;
      const current = ++generation.current;
      trigger.current =
        opener ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
      setRun(selected);
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
          setOptions(
            readLastRenderOptions({
              ...saved.defaults,
              outputDirectory: saved.defaults.outputDirectory || saved.outputDirectory,
            }),
          );
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
    try {
      const api = await renderer();
      const ready = await api.getHealth();
      setHealth(ready);
      if (!ready.orbitAvailable)
        throw new ApiError("Install and enable OrbitRender, then restart ADOFAI.", {
          kind: "domain",
          code: "orbit_missing",
        });
      if (ready.busy)
        throw new ApiError("Another render is running.", { kind: "domain", code: "render_busy" });
      if (cancelledRef.current) {
        setPhase("cancelled");
        return;
      }

      saveLastRenderOptions(options);
      setPhase("exporting");
      let bundle = await api.exportBundle(run.id, options, levelPath);
      exportId.current = bundle.jobId;
      while (bundle.state === "preparing" && !cancelledRef.current) {
        setProgress(bundle.progress);
        await pause();
        bundle = await api.getExportStatus(bundle.jobId);
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
        });
      }

      setPhase("preparing");
      setProgress(0);
      let rendered = await api.start(bundle.manifestPath, options);
      renderId.current = rendered.jobId;
      while (!renderJobFinished(rendered) && !cancelledRef.current) {
        setJob(rendered);
        setPhase(rendered.state);
        setProgress(rendered.progress);
        await pause();
        rendered = await api.getStatus(rendered.jobId);
      }
      if (!activeRef.current) return;
      setJob(rendered);
      setProgress(rendered.progress);
      setPhase(rendered.state);
      if (rendered.state === "failed") {
        setErrorCode(rendered.errorCode ?? null);
        setErrorMessage(rendered.errorMessage ?? "");
      }
    } catch (cause) {
      if (cancelledRef.current) {
        if (activeRef.current) setPhase("cancelled");
        return;
      }
      fail(cause);
    } finally {
      busyRef.current = false;
    }
  }, [run, options, levelPath, renderer, fail]);

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

  const busy =
    phase === "exporting" ||
    phase === "preparing" ||
    phase === "rendering" ||
    phase === "compositing";

  return {
    run,
    levelPath,
    options,
    setOptions,
    health,
    settings,
    phase,
    progress,
    job,
    errorCode,
    errorMessage,
    errorDetails,
    optionValidation,
    busy,
    usesOrbitRender: true,
    open,
    close: () => {
      if (busyRef.current) return;
      ++generation.current;
      setRun(null);
      requestAnimationFrame(() => {
        if (trigger.current?.isConnected) trigger.current.focus();
      });
    },
    start,
    cancel,
  };
}
