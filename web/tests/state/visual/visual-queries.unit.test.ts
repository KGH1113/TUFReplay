import { describe, expect, it } from "bun:test";
import { QueryClient } from "@tanstack/react-query";
import { visualKeys } from "@/state/visual/visual-queries";

describe("visual query account scope", () => {
  it("removes the old account while retaining the current account cache", () => {
    const client = new QueryClient();
    const accountA = "username:account-a";
    const accountB = "username:account-b";
    client.setQueryData(visualKeys.presets(accountA), { presets: [{ id: "a" }] });
    client.setQueryData(visualKeys.presets(accountB), { presets: [{ id: "b" }] });

    client.removeQueries({
      queryKey: visualKeys.all,
      predicate: (query) => query.queryKey[2] !== accountB,
    });

    expect(client.getQueryData(visualKeys.presets(accountA))).toBeUndefined();
    expect(client.getQueryData<unknown>(visualKeys.presets(accountB))).toEqual({
      presets: [{ id: "b" }],
    });
    client.clear();
  });
});
