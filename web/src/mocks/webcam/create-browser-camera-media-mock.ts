import type { BrowserCameraMedia } from "@/shared/clients/browser-camera-client";

export function createBrowserCameraMediaMock(): BrowserCameraMedia {
  let granted = false;
  const cameras = [
    { deviceId: "browser-mock-camera", label: "FaceTime HD Camera" },
    { deviceId: "browser-mock-usb-camera", label: "Mock USB Camera" },
  ];
  return {
    async enumerateDevices() {
      return cameras.map((camera) => ({
        ...camera,
        label: granted ? camera.label : "",
        kind: "videoinput",
        groupId: "mock",
        toJSON() {
          return camera;
        },
      })) as MediaDeviceInfo[];
    },
    async getUserMedia(constraints) {
      granted = true;
      const selected =
        typeof constraints.video === "object" &&
        typeof constraints.video.deviceId === "object" &&
        !Array.isArray(constraints.video.deviceId)
          ? constraints.video.deviceId.exact
          : undefined;
      const id = typeof selected === "string" ? selected : cameras[0].deviceId;
      if (!cameras.some((camera) => camera.deviceId === id)) {
        throw new DOMException("Unknown mock camera", "NotFoundError");
      }
      const canvas = document.createElement("canvas");
      canvas.width = 1280;
      canvas.height = 720;
      const context = canvas.getContext("2d");
      if (!context) throw new Error("Mock camera cannot draw");
      context.fillStyle = "#bc9b72";
      context.fillRect(0, 0, 1280, 720);
      context.fillStyle = "#27343c";
      context.fillRect(60, 0, 1160, 720);
      context.fillStyle = "#1c2931";
      context.fillRect(205, 130, 870, 390);
      for (let row = 0; row < 4; row++) {
        for (let column = 0; column < 12; column++) {
          context.fillStyle = row === 2 && column === 9 ? "#61c7ca" : "#8babb6";
          context.fillRect(230 + column * 68, 155 + row * 85, 58, 66);
        }
      }
      context.fillStyle = "#967352";
      context.fillRect(205, 550, 870, 135);
      const stream = canvas.captureStream(30);
      const track = stream.getVideoTracks()[0];
      const settings = track.getSettings.bind(track);
      Object.defineProperty(track, "getSettings", {
        value: () => ({ ...settings(), deviceId: id }),
      });
      (track as MediaStreamTrack & { requestFrame(): void }).requestFrame();
      return stream;
    },
  };
}
