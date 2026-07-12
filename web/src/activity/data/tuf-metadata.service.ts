import type { LevelMetadata } from "../activity.model";

const API_ROOT = "https://api.tuforums.com/v2/database";
const CACHE_NAME = "tuf-replay-metadata-v1";
const memory = new Map<number, Promise<LevelMetadata>>();
let difficultyCatalogRequest: Promise<Map<number, JsonRecord>> | null = null;

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
  const level = record(await fetchJson(`${API_ROOT}/levels/byId/${levelId}`, fetchImpl));
  if (!level) throw new Error("TUF level response was not an object");
  const difficultyId = numberValue(level, "diffId", "difficultyId", "DifficultyId", "difficulty_id");
  const embedded = record(value(level, "difficulty", "Difficulty"));
  const difficulty = embedded ?? (difficultyId === null ? null : (await getDifficultyCatalog(fetchImpl).catch(() => new Map())).get(difficultyId) ?? null);
  return {
    levelId,
    artist: text(level, "artist", "Artist", "songAuthor", "SongAuthor") || "Unknown artist",
    name: text(level, "song", "name", "Name", "levelName", "LevelName") || `Level #${levelId}`,
    creator: text(level, "creator", "Creator", "levelAuthor", "LevelAuthor") || "Unknown creator",
    difficulty: difficulty ? text(difficulty, "name", "Name", "displayName", "DisplayName") || "Unknown" : "Unknown",
    difficultyIconUrl: difficulty ? text(difficulty, "icon", "Icon", "iconUrl", "IconUrl") : "",
    source: "tuf",
  };
}

function getDifficultyCatalog(fetchImpl: typeof fetch): Promise<Map<number, JsonRecord>> {
  difficultyCatalogRequest ??= loadDifficultyCatalog(fetchImpl);
  return difficultyCatalogRequest;
}

async function loadDifficultyCatalog(fetchImpl: typeof fetch): Promise<Map<number, JsonRecord>> {
  const payload = await fetchJson(`${API_ROOT}/difficulties`, fetchImpl);
  if (!Array.isArray(payload)) throw new Error("TUF difficulty response was not an array");
  const catalog = new Map<number, JsonRecord>();
  for (const item of payload) {
    const difficulty = record(item);
    if (!difficulty) continue;
    const id = numberValue(difficulty, "id", "Id");
    if (id !== null) catalog.set(id, difficulty);
  }
  return catalog;
}

async function fetchJson(url: string, fetchImpl: typeof fetch): Promise<unknown> {
  const cache = "caches" in globalThis ? await caches.open(CACHE_NAME) : null;
  const cached = await cache?.match(url);
  if (cached) return unwrap(await cached.json());
  const response = await fetchImpl(url, { headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error(`TUF metadata request failed (${response.status})`);
  await cache?.put(url, response.clone());
  return unwrap(await response.json());
}

function unwrap(input: unknown): unknown {
  const root = record(input);
  if (!root) return input;
  return root.data ?? root.result ?? root;
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

function numberValue(input: JsonRecord, ...keys: string[]): number | null {
  const found = value(input, ...keys);
  if (typeof found === "number" && Number.isFinite(found)) return found;
  if (typeof found === "string" && found.trim() !== "" && Number.isFinite(Number(found))) return Number(found);
  return null;
}

export function clearTufMetadataMemoryCacheForTests() {
  memory.clear();
  difficultyCatalogRequest = null;
}
