import { useEffect, useMemo, useRef, useState } from "react";
import { useMockEnabled } from "@/api/app-api-provider";
import i18n from "@/i18n/i18n";
import { createBrowserCameraMediaMock } from "@/mocks/webcam/create-browser-camera-media-mock";
import { type BrowserCameraDevice, browserCameraErrorKey } from "@/models/webcam/browser-camera";
import { browserCameraMedia } from "@/shared/clients/browser-camera-client";
import { startBrowserCameraSession } from "@/state/webcam/browser-camera-session";

type PreviewRequest = { target: string; browserDeviceId?: string };
type PreviewStream = { request: PreviewRequest; stream: MediaStream };
type PreviewSize = { stream: MediaStream; width: number; height: number; ready: boolean };

export function useWebcamPreview(
  active: boolean,
  gameDeviceId: string | null,
  gameLabel: string | null,
) {
  const mockEnabled = useMockEnabled();
  const media = useMemo(
    () => (mockEnabled ? createBrowserCameraMediaMock() : browserCameraMedia()),
    [mockEnabled],
  );
  const videoRef = useRef<HTMLVideoElement>(null);
  const session = useRef<ReturnType<typeof startBrowserCameraSession> | null>(null);
  const [visible, setVisible] = useState(() => document.visibilityState === "visible");
  const [request, setRequest] = useState<PreviewRequest | null>(null);
  const [current, setCurrent] = useState<PreviewStream | null>(null);
  const [size, setSize] = useState<PreviewSize | null>(null);
  const [devices, setDevices] = useState<BrowserCameraDevice[]>([]);
  const [camera, setCamera] = useState<BrowserCameraDevice | null>(null);
  const [selectionRequired, setSelectionRequired] = useState(false);
  const [error, setError] = useState("");
  const target = JSON.stringify([gameDeviceId, gameLabel]);
  const revealed = active && visible && request?.target === target;
  const stream = revealed && current?.request === request ? current.stream : null;

  const stop = () => {
    session.current?.stop();
    session.current = null;
    if (videoRef.current) {
      videoRef.current.pause();
      videoRef.current.srcObject = null;
    }
  };

  useEffect(() => {
    const hide = () => {
      session.current?.stop();
      session.current = null;
      if (videoRef.current) {
        videoRef.current.pause();
        videoRef.current.srcObject = null;
      }
      setRequest(null);
      setCurrent(null);
    };
    const onVisibility = () => {
      const next = document.visibilityState === "visible";
      setVisible(next);
      if (!next) hide();
    };
    document.addEventListener("visibilitychange", onVisibility);
    window.addEventListener("pagehide", hide);
    return () => {
      document.removeEventListener("visibilitychange", onVisibility);
      window.removeEventListener("pagehide", hide);
    };
  }, []);

  useEffect(() => {
    if (!active) setRequest(null);
  }, [active]);

  useEffect(() => {
    if (request && request.target !== target) setRequest(null);
  }, [request, target]);

  useEffect(() => {
    if (!revealed || !request) return;
    setCurrent(null);
    setSize(null);
    setCamera(null);
    setSelectionRequired(false);
    setError("");
    if (!media) {
      setError(i18n.t("cropPreview.unsupported", { ns: "webcam" }));
      return;
    }
    const next = startBrowserCameraSession({
      media,
      gameDeviceId,
      gameLabel,
      browserDeviceId: request.browserDeviceId,
      onStream(nextStream, nextCamera) {
        setCamera(nextCamera);
        setCurrent({ request, stream: nextStream });
      },
      onDevices: setDevices,
      onSelectionRequired() {
        setSelectionRequired(true);
      },
      onError(cause) {
        setCurrent(null);
        setError(i18n.t(`cropPreview.${browserCameraErrorKey(cause)}`, { ns: "webcam" }));
      },
    });
    session.current = next;
    return () => {
      next.stop();
      if (session.current === next) session.current = null;
    };
  }, [media, revealed, request, gameDeviceId, gameLabel]);

  useEffect(() => {
    const video = videoRef.current;
    if (!video || !stream) return;
    let cancelled = false;
    video.srcObject = stream;
    void video.play().catch(() => {
      if (cancelled) return;
      session.current?.stop();
      setCurrent(null);
      setError(i18n.t("cropPreview.failed", { ns: "webcam" }));
    });
    return () => {
      cancelled = true;
      video.pause();
      video.srcObject = null;
    };
  }, [stream]);

  const begin = (browserDeviceId?: string) => {
    stop();
    setCurrent(null);
    setSize(null);
    setError("");
    setSelectionRequired(false);
    setRequest({ target, browserDeviceId });
  };

  return {
    videoRef,
    revealed,
    available: Boolean(stream && size?.stream === stream && size.ready),
    width: stream && size?.stream === stream ? size.width : 0,
    height: stream && size?.stream === stream ? size.height : 0,
    error: revealed ? error : "",
    camera: revealed ? camera : null,
    devices,
    selectionRequired: revealed && selectionRequired,
    selectedDeviceId: request?.browserDeviceId ?? "",
    reveal: () => begin(),
    retry: () => begin(request?.browserDeviceId),
    selectDevice: (id: string) => {
      if (id) begin(id);
    },
    hide() {
      stop();
      setRequest(null);
      setCurrent(null);
      setError("");
    },
    fail() {
      stop();
      setCurrent(null);
      setError(i18n.t("cropPreview.failed", { ns: "webcam" }));
    },
    updateSize() {
      const video = videoRef.current;
      if (!stream || !video || video.srcObject !== stream) return;
      setSize({
        stream,
        width: video.videoWidth,
        height: video.videoHeight,
        ready:
          video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA &&
          video.videoWidth > 0 &&
          video.videoHeight > 0,
      });
    },
  };
}
