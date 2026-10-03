import { describe, expect, test } from "bun:test";
import type { BrowserCameraDevice } from "@/models/webcam/browser-camera";
import type { BrowserCameraMedia } from "@/shared/clients/browser-camera-client";
import { startBrowserCameraSession } from "@/state/webcam/browser-camera-session";

function camera(id: string, label = "USB Camera"): MediaDeviceInfo {
  return { kind: "videoinput", deviceId: id, label } as MediaDeviceInfo;
}

function fakeStream(id: string) {
  let stops = 0;
  const events = new EventTarget();
  const track = {
    getSettings: () => ({ deviceId: id }),
    stop: () => {
      stops++;
    },
    readyState: "live",
    addEventListener: events.addEventListener.bind(events),
    removeEventListener: events.removeEventListener.bind(events),
  } as unknown as MediaStreamTrack;
  const stream = {
    getTracks: () => [track],
    getVideoTracks: () => [track],
  } as unknown as MediaStream;
  return { stream, stops: () => stops, ended: () => events.dispatchEvent(new Event("ended")) };
}

function observer() {
  const streams: MediaStream[] = [];
  const devices: BrowserCameraDevice[][] = [];
  const errors: unknown[] = [];
  let selections = 0;
  return {
    streams,
    devices,
    errors,
    selections: () => selections,
    callbacks: {
      onStream: (stream: MediaStream) => {
        streams.push(stream);
      },
      onDevices: (next: BrowserCameraDevice[]) => {
        devices.push(next);
      },
      onSelectionRequired: () => {
        selections++;
      },
      onError: (error: unknown) => {
        errors.push(error);
      },
    },
  };
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

describe("browser camera stream lifecycle", () => {
  test("requests video only and uses a matching browser ID instead of the native game ID", async () => {
    const calls: MediaStreamConstraints[] = [];
    const captured = fakeStream("browser-opaque");
    const observed = observer();
    const session = startBrowserCameraSession({
      media: {
        enumerateDevices: async () => [camera("browser-opaque")],
        getUserMedia: async (constraints) => {
          calls.push(constraints);
          return captured.stream;
        },
      },
      gameDeviceId: "native-camera-3",
      gameLabel: "USB Camera",
      ...observed.callbacks,
    });
    await session.done;
    expect(calls).toHaveLength(1);
    expect(calls[0].audio).toBe(false);
    expect(calls[0].video).toMatchObject({
      deviceId: { exact: "browser-opaque" },
      resizeMode: "none",
      frameRate: { ideal: 30 },
    });
    expect(observed.streams).toEqual([captured.stream]);
    session.stop();
    session.stop();
    expect(captured.stops()).toBe(1);
  });

  test("never publishes a permission probe and stops it before opening the matching camera", async () => {
    const probe = fakeStream("browser-default");
    const matched = fakeStream("browser-usb");
    const observed = observer();
    const calls: MediaStreamConstraints[] = [];
    let granted = false;
    const session = startBrowserCameraSession({
      media: {
        enumerateDevices: async () => [
          camera("browser-default", granted ? "Other Camera" : ""),
          camera("browser-usb", granted ? "USB Camera" : ""),
        ],
        getUserMedia: async (constraints) => {
          calls.push(constraints);
          granted = true;
          if (calls.length === 1) return probe.stream;
          expect(probe.stops()).toBe(1);
          expect(observed.streams).toHaveLength(0);
          return matched.stream;
        },
      },
      gameDeviceId: "native-usb",
      gameLabel: "USB Camera",
      ...observed.callbacks,
    });
    await session.done;
    expect(calls[0].video).not.toHaveProperty("deviceId");
    expect(calls[1].video).toHaveProperty("deviceId.exact", "browser-usb");
    expect(observed.streams).toEqual([matched.stream]);
    session.stop();
    expect(matched.stops()).toBe(1);
  });

  test.each([
    "missing",
    "ambiguous",
  ])("requires explicit preview selection when game label is %s", async (kind) => {
    const probe = fakeStream("default");
    const observed = observer();
    const available =
      kind === "missing" ? [camera("default", "Other Camera")] : [camera("one"), camera("two")];
    const session = startBrowserCameraSession({
      media: { enumerateDevices: async () => available, getUserMedia: async () => probe.stream },
      gameDeviceId: "native-id",
      gameLabel: "USB Camera",
      ...observed.callbacks,
    });
    await session.done;
    expect(observed.streams).toHaveLength(0);
    expect(observed.selections()).toBe(1);
    expect(probe.stops()).toBe(1);
    expect(observed.devices.at(-1)).toHaveLength(available.length);
    session.stop();
  });

  test("an explicit browser choice can open a camera whose label differs from the game", async () => {
    const observed = observer();
    const captured = fakeStream("chosen-browser-id");
    let constraints: MediaStreamConstraints | undefined;
    const session = startBrowserCameraSession({
      media: {
        enumerateDevices: async () => [camera("chosen-browser-id", "Different Browser Label")],
        getUserMedia: async (next) => {
          constraints = next;
          return captured.stream;
        },
      },
      gameDeviceId: "native-0",
      gameLabel: "Native Label",
      browserDeviceId: "chosen-browser-id",
      ...observed.callbacks,
    });
    await session.done;
    expect(constraints?.video).toHaveProperty("deviceId.exact", "chosen-browser-id");
    expect(observed.streams).toEqual([captured.stream]);
    expect(observed.selections()).toBe(0);
    session.stop();
  });

  test("the system-default selection uses the granted default stream directly", async () => {
    const captured = fakeStream("browser-default");
    const observed = observer();
    let requests = 0;
    const session = startBrowserCameraSession({
      media: {
        enumerateDevices: async () => [camera("browser-default", "Built-in Camera")],
        getUserMedia: async () => {
          requests++;
          return captured.stream;
        },
      },
      gameDeviceId: null,
      gameLabel: null,
      ...observed.callbacks,
    });
    await session.done;
    expect(requests).toBe(1);
    expect(observed.streams).toEqual([captured.stream]);
    session.stop();
  });

  test("stops a late permission grant after close without publishing it", async () => {
    const permission = deferred<MediaStream>();
    const started = deferred<void>();
    const captured = fakeStream("late-camera");
    const observed = observer();
    const session = startBrowserCameraSession({
      media: {
        enumerateDevices: async () => [],
        getUserMedia: () => {
          started.resolve();
          return permission.promise;
        },
      },
      gameDeviceId: null,
      gameLabel: null,
      ...observed.callbacks,
    });
    await started.promise;
    session.stop();
    permission.resolve(captured.stream);
    await session.done;
    expect(captured.stops()).toBe(1);
    expect(observed.streams).toHaveLength(0);
    expect(observed.errors).toHaveLength(0);
  });

  test("a replacement preview does not wait for an ignored permission prompt or stop its new stream", async () => {
    const permission = deferred<MediaStream>();
    const started = deferred<void>();
    const old = fakeStream("old");
    const replacement = fakeStream("new");
    let calls = 0;
    const media: BrowserCameraMedia = {
      enumerateDevices: async () => [],
      getUserMedia: () => {
        calls++;
        if (calls === 1) {
          started.resolve();
          return permission.promise;
        }
        return Promise.resolve(replacement.stream);
      },
    };
    const firstObserved = observer();
    const first = startBrowserCameraSession({
      media,
      gameDeviceId: null,
      gameLabel: null,
      ...firstObserved.callbacks,
    });
    await started.promise;
    first.stop();
    const nextObserved = observer();
    const next = startBrowserCameraSession({
      media,
      gameDeviceId: null,
      gameLabel: null,
      ...nextObserved.callbacks,
    });
    await next.done;
    permission.resolve(old.stream);
    await first.done;
    expect(firstObserved.streams).toHaveLength(0);
    expect(nextObserved.streams).toEqual([replacement.stream]);
    expect(old.stops()).toBe(1);
    expect(replacement.stops()).toBe(0);
    next.stop();
  });

  test.each([
    "devicechange",
    "ended",
  ])("stops all tracks and reports recovery on %s", async (event) => {
    const events = new EventTarget();
    const captured = fakeStream("camera");
    const observed = observer();
    const session = startBrowserCameraSession({
      media: {
        enumerateDevices: async () => [],
        getUserMedia: async () => captured.stream,
        addEventListener: events.addEventListener.bind(events),
        removeEventListener: events.removeEventListener.bind(events),
      },
      gameDeviceId: null,
      gameLabel: null,
      ...observed.callbacks,
    });
    await session.done;
    if (event === "ended") captured.ended();
    else events.dispatchEvent(new Event("devicechange"));
    expect(captured.stops()).toBe(1);
    expect(observed.errors).toHaveLength(1);
    events.dispatchEvent(new Event("devicechange"));
    session.stop();
    expect(observed.errors).toHaveLength(1);
    expect(captured.stops()).toBe(1);
  });

  test("stops the probe if device enumeration fails after permission", async () => {
    const captured = fakeStream("camera");
    const observed = observer();
    let enumerations = 0;
    const session = startBrowserCameraSession({
      media: {
        enumerateDevices: async () => {
          if (++enumerations > 1) throw new DOMException("Denied", "NotAllowedError");
          return [];
        },
        getUserMedia: async () => captured.stream,
      },
      gameDeviceId: "native",
      gameLabel: "Camera",
      ...observed.callbacks,
    });
    await session.done;
    expect(captured.stops()).toBe(1);
    expect(observed.errors).toHaveLength(1);
    expect(observed.streams).toHaveLength(0);
  });
});
