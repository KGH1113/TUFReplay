import defaults from "./services.json";

export function localOrigin(value: string): string {
  const url = new URL(value);
  if (url.protocol !== "http:" || !["127.0.0.1", "[::1]"].includes(url.hostname)
    || url.username || url.password || url.pathname !== "/" || url.search || url.hash)
    throw new Error("Local infrastructure addresses must be HTTP loopback origins");
  return url.origin;
}

export function storageEnvironment(services = defaults): Record<string, string> {
  return {
    TUF_REPLAY_STORAGE: "s3-local",
    R2_ENDPOINT: localOrigin(services.objectStore),
    R2_BUCKET: "tuf-replay-local",
    R2_ACCESS_KEY_ID: "S3RVER",
    R2_SECRET_ACCESS_KEY: "S3RVER",
    R2_LOCAL_FALLBACK: "false",
    REPLAY_CDN_ORIGIN: localOrigin(services.replayCdn),
    REPLAY_CDN_SIGNING_SECRET: "local-game-cdn-signing-secret-not-for-production",
  };
}

export async function requireStorage(services = defaults) {
  const env = storageEnvironment(services);
  try {
    const response = await fetch(`${env.REPLAY_CDN_ORIGIN}/__health`, { signal: AbortSignal.timeout(4000), redirect: "error" });
    if (!response.ok || (await response.json()).service !== "tuf-replay-local-cdn") throw new Error("CDN unavailable");
    const store = await fetch(env.R2_ENDPOINT, { signal: AbortSignal.timeout(4000), redirect: "error" });
    if (store.status !== 403 || store.headers.get("X-TUFReplay-Storage") !== "s3-local")
      throw new Error("Private S3 store unavailable");
  } catch (cause) {
    throw new Error("Local object storage/CDN is unavailable. Start Docker Desktop, then run ./scripts/run.sh live-infra up. Addresses: tools/live-e2e/services.json", { cause });
  }
}
