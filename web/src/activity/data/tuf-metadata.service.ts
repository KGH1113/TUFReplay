import type { LevelMetadata } from "../activity.model";

const API_ROOT = "https://api.tuforums.com/v2/database";
const CACHE_NAME = "tuf-replay-metadata-v1";
const memory = new Map<number, Promise<LevelMetadata>>();

type JsonRecord = Record<string, unknown>;

export function getFallbackMetadata(levelId: number | null): LevelMetadata {
  if (levelId === null) {
    return { levelId, artist: "Local level", name: "Custom level", creator: "Unknown creator", difficulty: "Local", difficultyIconUrl: "", source: "local" };
  }
  return { levelId, artist: "TUF database", name: `Level #${levelId}`, creator: "Unknown creator", difficulty: "Unknown", difficultyIconUrl: "", source: "fallback" };
}

export function getTufMetadata(levelId: number, fetchImpl: typeof fetch = fetch): Promise<LevelMetadata> {
  const existing = memory.get(levelId);
  if (existing) return existing;
  const request = loadMetadata(levelId, fetchImpl).catch(() => getFallbackMetadata(levelId));
  memory.set(levelId, request);
  return request;
}

async function loadMetadata(levelId: number, fetchImpl: typeof fetch): Promise<LevelMetadata> {
  const level = await fetchJson(`${API_ROOT}/levels/byId/${levelId}`, fetchImpl);
  const difficultyId = value(level, "difficultyId", "DifficultyId", "difficulty_id");
  const embedded = record(value(level, "difficulty", "Difficulty"));
  const difficulty = embedded ?? (difficultyId == null ? null : await fetchJson(`${API_ROOT}/difficulties/${difficultyId}`, fetchImpl));
  return {
    levelId,
    artist: text(level, "artist", "Artist", "songAuthor", "SongAuthor") || "Unknown artist",
    name: text(level, "name", "Name", "levelName", "LevelName") || `Level #${levelId}`,
    creator: text(level, "creator", "Creator", "levelAuthor", "LevelAuthor") || "Unknown creator",
    difficulty: difficulty ? text(difficulty, "name", "Name", "displayName", "DisplayName") || "Unknown" : "Unknown",
    difficultyIconUrl: difficulty ? text(difficulty, "icon", "Icon", "iconUrl", "IconUrl") : "",
    source: "tuf",
  };
}

async function fetchJson(url: string, fetchImpl: typeof fetch): Promise<JsonRecord> {
  const cache = "caches" in globalThis ? await caches.open(CACHE_NAME) : null;
  const cached = await cache?.match(url);
  if (cached) return unwrap(await cached.json());
  const response = await fetchImpl(url, { headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error(`TUF metadata request failed (${response.status})`);
  await cache?.put(url, response.clone());
  return unwrap(await response.json());
}

function unwrap(input: unknown): JsonRecord {
  const root = record(input) ?? {};
  return record(root.data) ?? record(root.result) ?? root;
}

function record(input: unknown): JsonRecord | null {
  return input !== null && typeof input === "object" && !Array.isArray(input) ? input as JsonRecord : null;
}

function value(input: JsonRecord, ...keys: string[]): unknown {
  for (const key of keys) if (key in input) return input[key];
  return undefined;
}

function text(input: JsonRecord, ...keys: string[]): string {
  const found = value(input, ...keys);
  return typeof found === "string" ? found : "";
}

export function clearTufMetadataMemoryCacheForTests() {
  memory.clear();
}
