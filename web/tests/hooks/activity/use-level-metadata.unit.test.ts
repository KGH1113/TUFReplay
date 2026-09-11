import { describe, expect, test } from "bun:test";
import { loadLevelMetadataBatches, mergeLevelMetadata } from "@/hooks/activity/use-level-metadata";
import type { LevelMetadata } from "@/models/activity/activity-model";

function metadata(levelId: number): LevelMetadata {
  return {
    levelId,
    artist: `artist-${levelId}`,
    name: `level-${levelId}`,
    creator: `creator-${levelId}`,
    difficulty: "Hard",
    difficultyIconUrl: "",
    source: "tuf",
  };
}

describe("TUF level metadata batching", () => {
  test("limits concurrency and applies one update per small batch", async () => {
    let active = 0;
    let maxActive = 0;
    const batches: number[][] = [];

    await loadLevelMetadataBatches(
      [1, 2, 3, 4, 5, 6, 7],
      async (levelId) => {
        active++;
        maxActive = Math.max(maxActive, active);
        await new Promise((resolve) => setTimeout(resolve, 1));
        active--;
        if (levelId === 3) throw new Error("unavailable");
        return metadata(levelId);
      },
      (batch) => batches.push([...batch.keys()]),
      3,
    );

    expect(maxActive).toBe(3);
    expect(batches).toEqual([[1, 2], [4, 5, 6], [7]]);
  });

  test("preserves map identity when a batch contains no metadata changes", () => {
    const value = metadata(1);
    const current = new Map([[1, value]]);

    expect(mergeLevelMetadata(current, new Map([[1, value]]))).toBe(current);
    const changed = mergeLevelMetadata(current, new Map([[1, { ...value, name: "updated" }]]));
    expect(changed).not.toBe(current);
    expect(changed.get(1)?.name).toBe("updated");
  });
});
