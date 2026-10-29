import { describe, expect, test } from "bun:test";
import { createBatchLoader } from "@/shared/lib/batch-loader";

describe("batch loader", () => {
  test("combines loads made together into one request and splits large batches", async () => {
    const requests: string[][] = [];
    const loader = createBatchLoader<string>(
      async (keys) => {
        requests.push(keys);
        return new Map(
          keys.filter((key) => key !== "missing").map((key) => [key, key.toUpperCase()]),
        );
      },
      { maxBatchSize: 2, delayMs: 0 },
    );
    const values = await Promise.all([
      loader.load("a"),
      loader.load("b"),
      loader.load("a"),
      loader.load("missing"),
    ]);
    expect(values).toEqual(["A", "B", "A", null]);
    expect(requests).toEqual([["a", "b"], ["missing"]]);
  });

  test("rejects every waiter of a failed request", async () => {
    const loader = createBatchLoader<string>(
      async () => {
        throw new Error("offline");
      },
      { delayMs: 0 },
    );
    await expect(loader.load("a")).rejects.toThrow("offline");
  });
});
