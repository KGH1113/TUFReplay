import type { CameraPreviewFrame } from "@/ports/camera-preview";

// One request at a time. The owner supplies I/O; the session owns cancellation
// and never publishes late frames after hide, camera change, or unmount.
export function startGameCameraSession(options: {
  read: (signal: AbortSignal) => Promise<CameraPreviewFrame | null>;
  onFrame: (frame: CameraPreviewFrame) => void;
  onError: (cause: unknown) => void;
  intervalMs?: number;
  timeoutMs?: number;
}) {
  const controller = new AbortController();
  let timer: ReturnType<typeof setTimeout> | undefined;
  let lastFrame = Date.now();
  const stop = () => {
    controller.abort();
    clearTimeout(timer);
  };
  const tick = async () => {
    if (controller.signal.aborted) return;
    const timeout = setTimeout(() => {
      if (controller.signal.aborted) return;
      stop();
      options.onError(new Error("Camera preview timed out"));
    }, options.timeoutMs ?? 10_000);
    try {
      const frame = await options.read(controller.signal);
      if (controller.signal.aborted) return;
      if (frame) {
        lastFrame = Date.now();
        options.onFrame(frame);
      } else if (Date.now() - lastFrame >= (options.timeoutMs ?? 10_000)) {
        throw new Error("Camera did not send a preview frame");
      }
      timer = setTimeout(() => void tick(), options.intervalMs ?? 125);
    } catch (cause) {
      if (controller.signal.aborted) return;
      stop();
      options.onError(cause);
    } finally {
      clearTimeout(timeout);
    }
  };
  void tick();
  return { stop };
}
