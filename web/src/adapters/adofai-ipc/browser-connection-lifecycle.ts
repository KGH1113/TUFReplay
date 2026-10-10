import type { IpcConnection } from "../../../vendor/adofai-ipc/src";

/** Browser resume can retain a socket without delivering its close event. */
export function observeBrowserConnection(
  connection: Pick<IpcConnection, "checkConnection">,
  browser: Pick<Window, "addEventListener" | "removeEventListener"> = window,
  page: Pick<Document, "addEventListener" | "removeEventListener" | "visibilityState"> = document,
): () => void {
  const resume = () => {
    if (page.visibilityState === "visible") connection.checkConnection();
  };
  const online = () => connection.checkConnection();
  browser.addEventListener("pageshow", resume);
  browser.addEventListener("focus", resume);
  browser.addEventListener("online", online);
  page.addEventListener("visibilitychange", resume);
  return () => {
    browser.removeEventListener("pageshow", resume);
    browser.removeEventListener("focus", resume);
    browser.removeEventListener("online", online);
    page.removeEventListener("visibilitychange", resume);
  };
}
