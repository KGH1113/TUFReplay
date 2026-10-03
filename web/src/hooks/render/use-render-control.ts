import { useCallback, useEffect, useRef, useState } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import type { RenderApi } from "@/api/render/render-api";
import type { ActivityRun } from "@/models/activity/activity-model";
import {
  defaultRenderOptions,
  type RenderHealth,
  type RenderJob,
  type RenderOptions,
  renderJobFinished,
} from "@/models/render/render-model";
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
  const [phase, setPhase] = useState<Phase>("idle");
  const [progress, setProgress] = useState(0);
  const [job, setJob] = useState<RenderJob | null>(null);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState("");
  const activeRef = useRef(true);
  const busyRef = useRef(false);
  const cancelledRef = useRef(false);
  const exportId = useRef<string | null>(null);
  const renderId = useRef<string | null>(null);
  const generation = useRef(0);
  const trigger = useRef<HTMLElement | null>(null);
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
    };
  }, []);
  const fail = useCallback((cause: unknown) => {
    if (!activeRef.current) return;
    setPhase("failed");
    setErrorCode(cause instanceof ApiError ? cause.code : null);
    setErrorMessage(cause instanceof Error ? cause.message : "");
  }, []);
  const open = useCallback(
    (selected: ActivityRun) => {
      if (busyRef.current) return;
      const current = ++generation.current;
      trigger.current =
        document.activeElement instanceof HTMLElement ? document.activeElement : null;
      setRun(selected);
      setHealth(null);
      setJob(null);
      setPhase("checking");
      setProgress(0);
      setErrorCode(null);
      setErrorMessage("");
      void renderer()
        .then((api) => api.getHealth())
        .then((state) => {
          if (!activeRef.current || current !== generation.current) return;
          setHealth(state);
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
      if (bundle.state !== "completed" || !bundle.manifestPath)
        throw new ApiError(bundle.errorMessage ?? "Export failed.", {
          kind: "domain",
          code: bundle.errorCode ?? "render_export_failed",
        });
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
      if (rendered.state === "failed") setErrorMessage(rendered.errorMessage ?? "");
    } catch (cause) {
      fail(cause);
    } finally {
      busyRef.current = false;
    }
  }, [run, options, renderer, fail]);
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
  const download = useCallback(async () => {
    if (!job?.canDownload) return;
    try {
      const url = await (await renderer()).prepareDownload(job.jobId);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = "";
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
    } catch (cause) {
      setErrorCode(cause instanceof ApiError ? cause.code : null);
      setErrorMessage(cause instanceof Error ? cause.message : "");
    }
  }, [job, renderer]);
  const busy =
    phase === "checking" ||
    phase === "exporting" ||
    phase === "preparing" ||
    phase === "rendering" ||
    phase === "compositing";
  return {
    run,
    options,
    setOptions,
    health,
    phase,
    progress,
    job,
    errorCode,
    errorMessage,
    busy,
    open,
    close: () => {
      if (!busyRef.current) {
        ++generation.current;
        setRun(null);
        requestAnimationFrame(() => {
          if (trigger.current?.isConnected) trigger.current.focus();
        });
      }
    },
    start,
    cancel,
    download,
  };
}
