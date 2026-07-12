import { describe, expect, test } from "bun:test";

import { ActivityDomainError, createActivityGateway, loadAllPages } from "./activity.gateway";

describe("activity IPC contract", () => {
  test("paging consumes raw arrays and continues past 1000 until a short page", async () => {
    const source = Array.from({ length: 1_237 }, (_, index) => index);
    const offsets: number[] = [];
    const result = await loadAllPages(async (offset, limit) => {
      offsets.push(offset);
      return source.slice(offset, offset + limit);
    });
    expect(result).toEqual(source);
    expect(offsets).toEqual([0, 200, 400, 600, 800, 1000, 1200]);
  });

  test("successful HTTP domain-error payloads become useful typed errors", async () => {
    const namespace = { call: async () => ({ error: { code: "not_found", message: "Session is missing" } }) };
    const gateway = createActivityGateway(namespace as never);
    try {
      await gateway.getLevelSession("missing");
      throw new Error("expected domain error");
    } catch (error) {
      expect(error).toBeInstanceOf(ActivityDomainError);
      expect((error as ActivityDomainError).code).toBe("not_found");
      expect((error as Error).message).toBe("Session is missing");
    }
  });
});
