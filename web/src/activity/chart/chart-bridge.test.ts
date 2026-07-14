import { describe, expect, mock, test } from "bun:test";

import type { ActivityChart } from "../activity.model";
import { ChartBridge, type ChartBridgeCallbacks } from "./chart-bridge";

const chart = { LevelSessionId: "l", LevelText: "{}", FloorCount: 10 } satisfies ActivityChart;

function setup() {
  const postMessage = mock((_message: unknown, _targetOrigin: string) => {});
  const source = { postMessage };
  const frame = { contentWindow: source } as unknown as HTMLIFrameElement;
  const callbacks = {
    onReady: mock(() => {}), onLoaded: mock(() => {}), onError: mock(() => {}),
    onFloorSelected: mock(() => {}), onMarkerSelected: mock(() => {}),
  } satisfies ChartBridgeCallbacks;
  const bridge = new ChartBridge(frame, "https://chart.example", callbacks);
  const send = (data: object, origin = "https://chart.example", eventSource: object = source) => bridge.handleMessage({ origin, source: eventSource, data: { protocol: "web-adofai.chart", version: 1, ...data } } as unknown as MessageEvent);
  return { bridge, callbacks, postMessage, send, source };
}

describe("ChartBridge child contract", () => {
  test("accepts chart.ready and emits exact command-specific correlation fields", () => {
    const { bridge, callbacks, postMessage, send } = setup();
    bridge.load(chart);
    bridge.setMarkers([{ id: "floor-2", floorIndex: 2, count: 1, clearCount: 0, bestLastFloorIndex: 5 }]);
    send({ type: "chart.ready" });
    expect(callbacks.onReady).toHaveBeenCalledTimes(1);
    const load = postMessage.mock.calls[0]?.[0] as Record<string, unknown>;
    const markers = postMessage.mock.calls[1]?.[0] as Record<string, unknown>;
    expect(load.type).toBe("chart.load"); expect(typeof load.requestId).toBe("string"); expect(load.revision).toBeUndefined();
    expect(markers.type).toBe("markers.set"); expect(markers.revision).toBe(1); expect(markers.requestId).toBeUndefined();
    bridge.focus(2, "floor-2");
    bridge.focusRun(2, 8);
    bridge.clearRunFocus();
    bridge.clear();
    expect(postMessage.mock.calls[2]?.[0]).toEqual({ protocol: "web-adofai.chart", version: 1, type: "chart.focus", floorIndex: 2, markerId: "floor-2" });
    expect(postMessage.mock.calls[3]?.[0]).toEqual({ protocol: "web-adofai.chart", version: 1, type: "run.focus", startFloorIndex: 2, endFloorIndex: 8 });
    expect(postMessage.mock.calls[4]?.[0]).toEqual({ protocol: "web-adofai.chart", version: 1, type: "run.clear" });
    expect(postMessage.mock.calls[5]?.[0]).toEqual({ protocol: "web-adofai.chart", version: 1, type: "chart.clear" });
  });

  test("does not send focus commands before the child is ready", () => {
    const { bridge, postMessage } = setup();
    bridge.focusRun(2, 8);
    bridge.fitEntireRun(2, 8);
    bridge.clearRunFocus();
    expect(postMessage).not.toHaveBeenCalled();
  });

  test("emits an exact additive v1 command when the user requests the entire run", () => {
    const { bridge, postMessage, send } = setup();
    send({ type: "chart.ready" });
    bridge.fitEntireRun(2, 8);
    expect(postMessage).toHaveBeenCalledTimes(1);
    expect(postMessage.mock.calls[0]?.[0]).toEqual({
      protocol: "web-adofai.chart",
      version: 1,
      type: "run.fit-all",
      startFloorIndex: 2,
      endFloorIndex: 8,
    });
    expect(postMessage.mock.calls[0]?.[1]).toBe("https://chart.example");
  });

  test("updates changed markers without reloading the chart and ignores equivalent polling data", () => {
    const { bridge, postMessage, send } = setup();
    const markers = [{ id: "floor-2", floorIndex: 2, count: 1, clearCount: 0, bestLastFloorIndex: 5 }];
    bridge.load(chart);
    bridge.setMarkers(markers);
    send({ type: "chart.ready" });
    postMessage.mockClear();

    bridge.setMarkers(markers.map((marker) => ({ ...marker })));
    expect(postMessage).not.toHaveBeenCalled();

    bridge.setMarkers([{ ...markers[0], count: 2 }]);
    expect(postMessage).toHaveBeenCalledTimes(1);
    expect(postMessage.mock.calls[0]?.[0]).toMatchObject({ type: "markers.set", markers: [{ ...markers[0], count: 2 }] });

    bridge.setMarkers([]);
    expect(postMessage).toHaveBeenCalledTimes(2);
    expect(postMessage.mock.calls[1]?.[0]).toMatchObject({ type: "markers.set", markers: [] });
  });

  test("restores the current chart and markers when the child becomes ready again", () => {
    const { bridge, callbacks, postMessage, send } = setup();
    bridge.load(chart);
    bridge.setMarkers([{ id: "floor-2", floorIndex: 2, count: 1, clearCount: 0, bestLastFloorIndex: 5 }]);
    send({ type: "chart.ready" });
    postMessage.mockClear();

    send({ type: "chart.ready" });
    expect(callbacks.onReady).toHaveBeenCalledTimes(2);
    expect(postMessage.mock.calls.map((call) => (call[0] as { type: string }).type)).toEqual(["chart.load", "markers.set"]);
  });

  test("correlates loaded/error by requestId only and rejects stale or malformed loads", () => {
    const { bridge, callbacks, postMessage, send } = setup();
    bridge.load(chart); bridge.setMarkers([]); send({ type: "chart.ready" });
    const requestId = (postMessage.mock.calls[0]?.[0] as { requestId: string }).requestId;
    send({ type: "chart.loaded", requestId: "stale", floorCount: 10 });
    send({ type: "chart.loaded", requestId, floorCount: "10" });
    expect(callbacks.onLoaded).not.toHaveBeenCalled();
    send({ type: "chart.loaded", requestId, floorCount: 10 });
    expect(callbacks.onLoaded).toHaveBeenCalledTimes(1);
    send({ type: "chart.error", requestId, code: "parse", message: "bad chart" });
    expect(callbacks.onError).toHaveBeenCalledWith("bad chart");
  });

  test("accepts typed selection events without correlation fields and reads marker id", () => {
    const { callbacks, send } = setup();
    send({ type: "chart.floor-selected", floorIndex: 7 });
    send({ type: "chart.marker-selected", id: "floor-7", floorIndex: 7 });
    send({ type: "chart.marker-selected", markerId: "wrong", floorIndex: 7 });
    expect(callbacks.onFloorSelected).toHaveBeenCalledWith(7);
    expect(callbacks.onMarkerSelected).toHaveBeenCalledTimes(1);
    expect(callbacks.onMarkerSelected).toHaveBeenCalledWith("floor-7", 7);
  });

  test("rejects foreign origin, source, protocol, and version", () => {
    const { callbacks, send } = setup();
    send({ type: "chart.ready" }, "https://evil.example");
    send({ type: "chart.ready" }, "https://chart.example", {});
    send({ type: "chart.ready", protocol: "wrong" });
    send({ type: "chart.ready", version: 2 });
    expect(callbacks.onReady).not.toHaveBeenCalled();
  });
});
