import { useEffect, useRef, useState } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import i18n from "@/i18n/i18n";
import { startGameCameraSession } from "@/state/webcam/game-camera-session";

type Request = { target: string };
type Frame = { request: Request; url: string; width: number; height: number };

export function useWebcamPreview(
  active: boolean,
  gameDeviceId: string | null,
  gameLabel: string | null,
) {
  const api = useApiPromise();
  const session = useRef<ReturnType<typeof startGameCameraSession> | null>(null);
  const [visible, setVisible] = useState(() => document.visibilityState === "visible");
  const [request, setRequest] = useState<Request | null>(null);
  const [frame, setFrame] = useState<Frame | null>(null);
  const [loaded, setLoaded] = useState<Request | null>(null);
  const [error, setError] = useState("");
  const target = JSON.stringify([gameDeviceId, gameLabel]);
  const revealed = active && visible && request?.target === target;
  const current = revealed && frame?.request === request ? frame : null;

  useEffect(() => {
    const hide = () => {
      session.current?.stop();
      setRequest(null);
    };
    const visibility = () => {
      const next = document.visibilityState === "visible";
      setVisible(next);
      if (!next) hide();
    };
    document.addEventListener("visibilitychange", visibility);
    window.addEventListener("pagehide", hide);
    return () => {
      document.removeEventListener("visibilitychange", visibility);
      window.removeEventListener("pagehide", hide);
    };
  }, []);

  useEffect(() => {
    if (!active || request?.target !== target) setRequest(null);
  }, [active, target, request]);

  useEffect(() => {
    if (!revealed || !request) return;
    setFrame(null);
    setLoaded(null);
    setError("");
    let url = "";
    const next = startGameCameraSession({
      async read(signal) {
        const app = await api;
        if (signal.aborted) return null;
        return app.webcam.getPreviewFrame(gameDeviceId, { signal });
      },
      onFrame(nextFrame) {
        const previous = url;
        url = URL.createObjectURL(new Blob([nextFrame.bytes], { type: "image/bmp" }));
        setFrame({ request, url, width: nextFrame.width, height: nextFrame.height });
        if (previous) URL.revokeObjectURL(previous);
      },
      onError() {
        setFrame(null);
        setError(i18n.t("cropPreview.failed", { ns: "webcam" }));
      },
    });
    session.current = next;
    return () => {
      next.stop();
      if (session.current === next) session.current = null;
      if (url) URL.revokeObjectURL(url);
    };
  }, [api, revealed, request, gameDeviceId]);

  return {
    revealed,
    imageUrl: current?.url,
    available: Boolean(current && loaded === request),
    width: current?.width ?? 0,
    height: current?.height ?? 0,
    error: revealed ? error : "",
    label: revealed ? gameLabel : null,
    reveal() {
      setRequest({ target });
    },
    retry() {
      session.current?.stop();
      setRequest({ target });
    },
    hide() {
      session.current?.stop();
      setRequest(null);
    },
    fail() {
      session.current?.stop();
      setFrame(null);
      setError(i18n.t("cropPreview.failed", { ns: "webcam" }));
    },
    imageLoaded() {
      if (current) setLoaded(current.request);
    },
  };
}
