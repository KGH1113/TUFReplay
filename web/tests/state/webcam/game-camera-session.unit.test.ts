import { describe, expect, test } from "bun:test";
import { startGameCameraSession } from "@/state/webcam/game-camera-session";

const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms));
const frame = { bytes: new Uint8Array(1), width: 2, height: 2 };

describe("shared game camera session", () => {
  test("serializes reads and aborts a pending frame without publishing after hide", async () => {
    let resolve!: (value: typeof frame) => void;
    let signal!: AbortSignal;
    let reads = 0;
    let published = 0;
    const session = startGameCameraSession({
      read(nextSignal) {
        signal = nextSignal;
        reads++;
        return new Promise((next) => {
          resolve = next;
        });
      },
      onFrame() {
        published++;
      },
      onError() {
        throw new Error("Unexpected error");
      },
      intervalMs: 1,
    });
    await wait();
    expect(reads).toBe(1);
    session.stop();
    expect(signal.aborted).toBe(true);
    resolve(frame);
    await wait();
    expect(published).toBe(0);
    expect(reads).toBe(1);
  });

  test("a hidden stale request does not block a replacement camera", async () => {
    const stale = startGameCameraSession({
      read: () => new Promise(() => {}),
      onFrame() {},
      onError() {},
    });
    stale.stop();
    let frames = 0;
    const next = startGameCameraSession({
      read: async () => frame,
      onFrame() {
        frames++;
      },
      onError() {},
      intervalMs: 1,
    });
    await wait();
    next.stop();
    expect(frames).toBeGreaterThan(0);
  });

  test("reports a connection failure once and stops polling", async () => {
    let reads = 0;
    let errors = 0;
    const session = startGameCameraSession({
      read: async () => {
        reads++;
        throw new Error("Disconnected");
      },
      onFrame() {},
      onError() {
        errors++;
      },
      intervalMs: 1,
    });
    await wait();
    session.stop();
    expect(reads).toBe(1);
    expect(errors).toBe(1);
  });

  test("warmup without frames times out and releases the request", async () => {
    let errors = 0;
    const session = startGameCameraSession({
      read: async () => null,
      onFrame() {},
      onError() {
        errors++;
      },
      intervalMs: 1,
      timeoutMs: 5,
    });
    await wait(25);
    session.stop();
    expect(errors).toBe(1);
  });
});
