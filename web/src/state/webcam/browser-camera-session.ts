import {
  type BrowserCameraDevice,
  browserCameraDevices,
  matchBrowserCamera,
} from "@/models/webcam/browser-camera";
import type { BrowserCameraMedia } from "@/shared/clients/browser-camera-client";

type CameraSessionOptions = {
  media: BrowserCameraMedia;
  gameDeviceId: string | null;
  gameLabel: string | null;
  browserDeviceId?: string;
  onStream: (stream: MediaStream, camera: BrowserCameraDevice | null) => void;
  onDevices: (devices: BrowserCameraDevice[]) => void;
  onSelectionRequired: () => void;
  onError: (error: unknown) => void;
};

export function startBrowserCameraSession(options: CameraSessionOptions) {
  let stopped = false;
  const owned = new Set<MediaStream>();
  const stoppedTracks = new WeakSet<MediaStreamTrack>();
  const endedListeners: Array<{ track: MediaStreamTrack; listener: EventListener }> = [];

  const stopStream = (stream: MediaStream) => {
    owned.delete(stream);
    for (const track of stream.getTracks()) {
      if (!stoppedTracks.has(track)) {
        stoppedTracks.add(track);
        track.stop();
      }
    }
  };
  const stop = () => {
    stopped = true;
    options.media.removeEventListener?.("devicechange", disconnected);
    for (const { track, listener } of endedListeners) track.removeEventListener("ended", listener);
    endedListeners.length = 0;
    for (const stream of owned) stopStream(stream);
  };
  const disconnected = () => {
    if (stopped) return;
    stop();
    options.onError(new DOMException("Camera disconnected", "NotFoundError"));
  };
  const acquire = async (id?: string) => {
    const video: MediaTrackConstraints & { resizeMode: "none" } = {
      ...(id ? { deviceId: { exact: id } } : {}),
      width: { ideal: 1280 },
      height: { ideal: 720 },
      frameRate: { ideal: 30 },
      resizeMode: "none",
    };
    const stream = await options.media.getUserMedia({
      audio: false,
      video,
    });
    owned.add(stream);
    if (stopped) {
      stopStream(stream);
      return null;
    }
    return stream;
  };
  const devices = async () => {
    const result = browserCameraDevices(await options.media.enumerateDevices());
    if (!stopped) options.onDevices(result);
    return result;
  };
  const publish = (stream: MediaStream, camera: BrowserCameraDevice | null) => {
    if (stopped) return;
    const tracks = stream.getVideoTracks();
    if (!tracks.length || tracks.some((track) => track.readyState === "ended")) {
      disconnected();
      return;
    }
    for (const track of tracks) {
      track.addEventListener("ended", disconnected);
      endedListeners.push({ track, listener: disconnected });
    }
    options.onStream(stream, camera);
  };

  options.media.addEventListener?.("devicechange", disconnected);
  const done = (async () => {
    try {
      let available = await devices();
      if (stopped) return;
      let target = options.browserDeviceId
        ? (available.find((device) => device.id === options.browserDeviceId) ?? {
            id: options.browserDeviceId,
            label: "",
          })
        : options.gameDeviceId
          ? matchBrowserCamera(available, options.gameLabel)
          : null;
      if (target) {
        const stream = await acquire(target.id);
        if (stream) publish(stream, target);
        return;
      }

      // Grant labels without ever rendering a default camera for an explicit game selection.
      const probe = await acquire();
      if (!probe) return;
      available = await devices();
      if (stopped) return;
      if (!options.gameDeviceId) {
        const actualId = probe.getVideoTracks()[0]?.getSettings().deviceId;
        publish(probe, available.find((device) => device.id === actualId) ?? null);
        return;
      }
      target = matchBrowserCamera(available, options.gameLabel);
      if (!target) {
        stopStream(probe);
        options.onSelectionRequired();
        return;
      }
      if (probe.getVideoTracks()[0]?.getSettings().deviceId === target.id) {
        publish(probe, target);
        return;
      }
      stopStream(probe);
      const stream = await acquire(target.id);
      if (stream) publish(stream, target);
    } catch (error) {
      if (stopped) return;
      stop();
      options.onError(error);
    }
  })();
  return { stop, done };
}
