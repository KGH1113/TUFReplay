import { expect, test } from "bun:test";

import { installVisiblePolling } from "./use-visible-polling.hook";

test("polling stops while hidden and refreshes immediately when visible", () => {
  const original = globalThis.document;
  let listener: (() => void) | undefined;
  const fake = {
    visibilityState: "visible",
    addEventListener: (_: string, value: () => void) => {
      listener = value;
    },
    removeEventListener: () => {},
  };
  Object.defineProperty(globalThis, "document", { configurable: true, value: fake });
  let calls = 0;
  const cleanup = installVisiblePolling(() => {
    calls += 1;
  }, 60_000);
  expect(calls).toBe(1);
  fake.visibilityState = "hidden";
  listener?.();
  fake.visibilityState = "visible";
  listener?.();
  expect(calls).toBe(2);
  cleanup();
  Object.defineProperty(globalThis, "document", { configurable: true, value: original });
});
