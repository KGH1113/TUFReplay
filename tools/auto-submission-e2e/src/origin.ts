import { uiPort } from "./config";

const loopbackHosts = new Set(["127.0.0.1", "localhost", "[::1]"]);

export function isAllowedOrigin(origin: string) {
  try {
    const url = new URL(origin);
    if (url.protocol !== "http:" || !loopbackHosts.has(url.hostname)) return false;
    return url.port === String(uiPort) || url.port === "5152";
  } catch {
    return false;
  }
}
