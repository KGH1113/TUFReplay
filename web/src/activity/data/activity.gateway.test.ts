import { expect, test } from "bun:test";

import { loadAllPages } from "./activity.gateway";

test("paging continues past 1000 until a short page", async () => {
  const source = Array.from({ length: 1_237 }, (_, index) => index);
  const offsets: number[] = [];
  const result = await loadAllPages(async (offset, limit) => {
    offsets.push(offset);
    return { Items: source.slice(offset, offset + limit) };
  });
  expect(result).toEqual(source);
  expect(offsets).toEqual([0, 200, 400, 600, 800, 1000, 1200]);
});
