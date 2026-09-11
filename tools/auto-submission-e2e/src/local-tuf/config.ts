import { readFileSync } from "node:fs";

export const localTuf = process.env.E2E_TUF_TARGET === "local";
if (process.env.E2E_TUF_TARGET && !["local", "mock"].includes(process.env.E2E_TUF_TARGET))
  throw new Error("E2E_TUF_TARGET must be mock or local");

// Deliberately fixed: this mode cannot register passes on a remote service.
export const localTufBase = "http://127.0.0.1:3002";
export const localFrontendBase = "http://127.0.0.1:5176";
export function localAccessToken(): string {
  const path = new URL("../../.data/local-tuf-access-token", import.meta.url);
  const token = readFileSync(path, "utf8").trim();
  if (!token) throw new Error("Prepare the local TUF OAuth fixture first");
  return token;
}
