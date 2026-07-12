import { expect, mock, test } from "bun:test";

import type { ActivityChart } from "../activity.model";
import { ChartBridge } from "./chart-bridge";

test("bridge rejects foreign sources/origins and stale chart responses", () => {
  const postMessage = mock(() => {});
  const source = { postMessage };
  const frame = { contentWindow: source } as unknown as HTMLIFrameElement;
  const loaded = mock(() => {});
  const bridge = new ChartBridge(frame, "https://chart.example", { onReady: () => {}, onLoaded: loaded, onError: () => {}, onFloorSelected: () => {}, onMarkerSelected: () => {} });
  bridge.handleMessage({ origin: "https://evil.example", source, data: { protocol: "web-adofai.chart", version: 1, type: "ready" } } as unknown as MessageEvent);
  expect(postMessage).not.toHaveBeenCalled();
  bridge.load({ LevelSessionId: "l", LevelText: "{}", FloorCount: 10 } satisfies ActivityChart, []);
  bridge.handleMessage({ origin: "https://chart.example", source, data: { protocol: "web-adofai.chart", version: 1, type: "ready" } } as unknown as MessageEvent);
  expect(postMessage).toHaveBeenCalledTimes(2);
  bridge.handleMessage({ origin: "https://chart.example", source, data: { protocol: "web-adofai.chart", version: 1, type: "chart.loaded", requestId: "stale", revision: 0 } } as unknown as MessageEvent);
  expect(loaded).not.toHaveBeenCalled();
});
