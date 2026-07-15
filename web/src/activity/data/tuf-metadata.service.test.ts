import { beforeEach, describe, expect, test } from "bun:test";

import { clearTufMetadataMemoryCacheForTests, getTufMetadata } from "./tuf-metadata.service";

const API_ROOT = "/api/tuf/v2/database";

describe("TUF metadata live API contract", () => {
  beforeEach(clearTufMetadataMemoryCacheForTests);

  test("reads song/diffId and resolves difficulty from the catalog endpoint", async () => {
    const urls: string[] = [];
    const fetcher = createFetcher(urls, {
      [`${API_ROOT}/levels/byId/3118`]: {
        id: 3118,
        song: "First Town Of This Journey",
        artist: "Camellia (かめりあ)",
        creator: "Appeal",
        diffId: 17,
      },
      [`${API_ROOT}/difficulties`]: [
        { id: 17, name: "P17", icon: "https://api.tuforums.com/icon.png" },
      ],
    });

    await expect(getTufMetadata(3118, fetcher)).resolves.toEqual({
      levelId: 3118,
      name: "First Town Of This Journey",
      artist: "Camellia (かめりあ)",
      creator: "Appeal",
      difficulty: "P17",
      difficultyIconUrl: "https://api.tuforums.com/icon.png",
      source: "tuf",
    });
    expect(urls).toEqual([`${API_ROOT}/levels/byId/3118`, `${API_ROOT}/difficulties`]);
    expect(urls.some((url) => url.includes("/difficulties/17"))).toBeFalse();
  });

  test("deduplicates levels and the shared difficulty catalog across concurrent requests", async () => {
    const urls: string[] = [];
    const fetcher = createFetcher(urls, {
      [`${API_ROOT}/levels/byId/3118`]: {
        id: 3118,
        song: "First",
        artist: "A",
        creator: "C",
        diffId: 17,
      },
      [`${API_ROOT}/levels/byId/5376`]: {
        id: 5376,
        song: "HALL",
        artist: "Frums",
        creator: "한가지",
        diffId: 21,
      },
      [`${API_ROOT}/difficulties`]: [
        { id: 17, name: "P17", icon: "p17.png" },
        { id: 21, name: "U15", icon: "u15.png" },
      ],
    });

    const [first, duplicate, second] = await Promise.all([
      getTufMetadata(3118, fetcher),
      getTufMetadata(3118, fetcher),
      getTufMetadata(5376, fetcher),
    ]);
    expect(first).toEqual(duplicate);
    expect(second.difficulty).toBe("U15");
    expect(urls.filter((url) => url === `${API_ROOT}/levels/byId/3118`)).toHaveLength(1);
    expect(urls.filter((url) => url === `${API_ROOT}/difficulties`)).toHaveLength(1);
  });

  test("keeps level metadata with unknown difficulty when the catalog is unavailable", async () => {
    const urls: string[] = [];
    const fetcher = createFetcher(urls, {
      [`${API_ROOT}/levels/byId/3118`]: {
        id: 3118,
        song: "First Town",
        artist: "Camellia",
        creator: "Appeal",
        diffId: 17,
      },
    });
    const metadata = await getTufMetadata(3118, fetcher);
    expect(metadata.name).toBe("First Town");
    expect(metadata.difficulty).toBe("Unknown");
    expect(metadata.source).toBe("tuf");
  });
});

function createFetcher(urls: string[], responses: Record<string, unknown>): typeof fetch {
  return (async (input: string | URL | Request) => {
    const url = String(input);
    urls.push(url);
    if (!(url in responses)) return new Response("not found", { status: 404 });
    return new Response(JSON.stringify(responses[url]), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  }) as unknown as typeof fetch;
}
