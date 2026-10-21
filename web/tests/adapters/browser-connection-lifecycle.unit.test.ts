import { expect, test } from "bun:test";
import { observeBrowserConnection } from "@/adapters/adofai-ipc/browser-connection-lifecycle";

test("a cached page retains its app, and a later actual departure disposes it", () => {
  const browser = new EventTarget();
  const page = Object.assign(new EventTarget(), { visibilityState: "visible" as const });
  let disposed = 0;
  let checked = 0;
  observeBrowserConnection(
    { checkConnection: () => checked++, dispose: () => disposed++ },
    browser,
    page,
  );
  browser.dispatchEvent(Object.assign(new Event("pagehide"), { persisted: true }));
  browser.dispatchEvent(new Event("pageshow"));
  expect(disposed).toBe(0);
  expect(checked).toBe(1);
  browser.dispatchEvent(Object.assign(new Event("pagehide"), { persisted: false }));
  browser.dispatchEvent(new Event("pageshow"));
  expect(disposed).toBe(1);
  expect(checked).toBe(1);
});

test("browser resume checks the retained session and cleanup releases every listener", () => {
  const browser = new EventTarget();
  const page = Object.assign(new EventTarget(), {
    visibilityState: "hidden" as DocumentVisibilityState,
  });
  let checks = 0;
  const stop = observeBrowserConnection({ checkConnection: () => checks++ }, browser, page);
  page.dispatchEvent(new Event("visibilitychange"));
  browser.dispatchEvent(new Event("focus"));
  expect(checks).toBe(0);
  page.visibilityState = "visible";
  page.dispatchEvent(new Event("visibilitychange"));
  browser.dispatchEvent(new Event("pageshow"));
  browser.dispatchEvent(new Event("focus"));
  browser.dispatchEvent(new Event("online"));
  expect(checks).toBe(4);
  stop();
  for (const name of ["pageshow", "focus", "online"]) browser.dispatchEvent(new Event(name));
  page.dispatchEvent(new Event("visibilitychange"));
  expect(checks).toBe(4);
});
