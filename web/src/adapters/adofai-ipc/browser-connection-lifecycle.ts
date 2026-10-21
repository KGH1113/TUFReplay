/** Browser resume can retain a socket without delivering its close event. */
export function observeBrowserConnection(
  connection: { checkConnection(): void; dispose?(): void },
  browser: Pick<Window, "addEventListener" | "removeEventListener"> = window,
  page: Pick<Document, "addEventListener" | "removeEventListener" | "visibilityState"> = document,
): () => void {
  const resume = () => {
    if (page.visibilityState === "visible") connection.checkConnection();
  };
  const online = () => connection.checkConnection();
  const leave = (event: Event) => {
    if (!(event as PageTransitionEvent).persisted && connection.dispose) {
      stop();
      connection.dispose();
    }
  };
  const stop = () => {
    browser.removeEventListener("pageshow", resume);
    browser.removeEventListener("focus", resume);
    browser.removeEventListener("online", online);
    browser.removeEventListener("pagehide", leave);
    page.removeEventListener("visibilitychange", resume);
  };
  browser.addEventListener("pageshow", resume);
  browser.addEventListener("focus", resume);
  browser.addEventListener("online", online);
  browser.addEventListener("pagehide", leave);
  page.addEventListener("visibilitychange", resume);
  return stop;
}
