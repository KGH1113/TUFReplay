import { describe, expect, test } from "bun:test";
import {
  browserCameraDevices,
  browserCameraErrorKey,
  matchBrowserCamera,
} from "@/models/webcam/browser-camera";

describe("browser camera matching", () => {
  test("matches only a unique camera label without using a game's device ID", () => {
    const devices = [
      { id: "browser-opaque", label: " USB   Camera " },
      { id: "other", label: "FaceTime HD Camera" },
    ];
    expect(matchBrowserCamera(devices, "usb camera")?.id).toBe("browser-opaque");
    expect(matchBrowserCamera(devices, "USB Camera (abcd:1234)")).toBe(null);
    expect(matchBrowserCamera(devices, null)).toBe(null);
    expect(
      matchBrowserCamera([...devices, { id: "duplicate", label: "USB Camera" }], "USB Camera"),
    ).toBe(null);
    expect(matchBrowserCamera([{ id: "hidden", label: "" }], "USB Camera")).toBe(null);
    expect(matchBrowserCamera([{ id: "hidden", label: "" }], "   ")).toBe(null);
  });

  test("exposes video cameras only and preserves opaque browser IDs", () => {
    const devices = [
      { kind: "videoinput", deviceId: "browser-1", label: "Camera" },
      { kind: "audioinput", deviceId: "mic", label: "Microphone" },
      { kind: "videoinput", deviceId: "", label: "Unavailable" },
    ] as MediaDeviceInfo[];
    expect(browserCameraDevices(devices)).toEqual([{ id: "browser-1", label: "Camera" }]);
  });

  test.each([
    ["NotAllowedError", "permission"],
    ["SecurityError", "permission"],
    ["NotFoundError", "notFound"],
    ["OverconstrainedError", "notFound"],
    ["NotReadableError", "inUse"],
    ["AbortError", "inUse"],
    ["UnknownError", "failed"],
  ])("maps %s to user-facing recovery %s", (name, expected) => {
    expect(browserCameraErrorKey(new DOMException("Internal details", name))).toBe(expected);
  });
});
