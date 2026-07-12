import { expect, test } from "bun:test";

import { clearTufMetadataMemoryCacheForTests, getTufMetadata } from "./tuf-metadata.service";

test("metadata requests are deduplicated in memory", async () => {
  clearTufMetadataMemoryCacheForTests();
  let calls = 0;
  const fetcher = (async () => { calls += 1; return new Response(JSON.stringify({ name: "Level", artist: "Artist", creator: "Creator" }), { status: 200 }); }) as unknown as typeof fetch;
  const [first, second] = await Promise.all([getTufMetadata(42, fetcher), getTufMetadata(42, fetcher)]);
  expect(calls).toBe(1);
  expect(first).toEqual(second);
});
