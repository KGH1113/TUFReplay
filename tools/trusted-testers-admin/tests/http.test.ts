import { describe, expect, test } from "bun:test";
import { createAdminHandler } from "../src/http.controller";
import { lookupLinkedPlayer } from "../src/player-lookup";
import type { Tester, TesterEvent, TesterRepository } from "../src/testers.model";

const id = "670cac2c-8175-46a6-87f7-b92741d4499f";
const credentials = { username: "operator", password: "a".repeat(32) };
const authorization = `Basic ${btoa(`${credentials.username}:${credentials.password}`)}`;

function fakeRepository(): TesterRepository & { testers: Tester[]; events: TesterEvent[] } {
  const testers: Tester[] = [];
  const events: TesterEvent[] = [];
  return {
    testers,
    events,
    async list() {
      return { testers: [...testers], events: [...events] };
    },
    async grant(userId, label, actor, reason) {
      const now = new Date().toISOString();
      const row = { userId, label, active: true, updatedAt: now, updatedBy: actor };
      const index = testers.findIndex((item) => item.userId === userId);
      if (index < 0) testers.push(row);
      else testers[index] = row;
      events.push({ id: String(events.length + 1), userId, action: "grant", actor, reason, createdAt: now });
      return row;
    },
    async revoke(userId, actor, reason) {
      const row = testers.find((item) => item.userId === userId && item.active);
      if (!row) return null;
      row.active = false;
      row.updatedAt = new Date().toISOString();
      row.updatedBy = actor;
      events.push({ id: String(events.length + 1), userId, action: "revoke", actor, reason, createdAt: row.updatedAt });
      return row;
    },
  };
}

function request(path: string, method = "GET", body?: unknown, headers: Record<string, string> = {}) {
  return new Request(`http://127.0.0.1:4177${path}`, {
    method,
    headers: {
      authorization,
      ...(body === undefined ? {} : { "Content-Type": "application/json", Origin: "http://127.0.0.1:4177" }),
      ...headers,
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
}

describe("trusted tester administration", () => {
  test("requires administrator credentials before serving the UI or roster", async () => {
    const handler = createAdminHandler(fakeRepository(), credentials, { html: "private", css: "", javascript: "" });
    const response = await handler(new Request("http://127.0.0.1:4177/api/testers"));
    expect(response.status).toBe(401);
    expect(response.headers.get("WWW-Authenticate")).toContain("Basic");
    expect((await response.text()).includes("private")).toBe(false);
    expect((await handler(request("/"))).status).toBe(200);
  });

  test("rejects cross-origin, malformed, and incomplete mutations", async () => {
    const repository = fakeRepository();
    const handler = createAdminHandler(repository, credentials, { html: "", css: "", javascript: "" });
    expect((await handler(request("/api/testers", "POST", { userId: id, label: "Impl", reason: "Beta" }, { Origin: "http://evil.test" }))).status).toBe(403);
    expect((await handler(request("/api/testers", "POST", { userId: "7410", label: "Impl", reason: "Beta" }))).status).toBe(400);
    expect((await handler(request("/api/testers", "POST", { userId: id, label: "Impl", reason: "" }))).status).toBe(400);
    expect(repository.testers).toHaveLength(0);
  });

  test("grants, lists, revokes, and preserves the change trail", async () => {
    const repository = fakeRepository();
    const handler = createAdminHandler(repository, credentials, { html: "", css: "", javascript: "" });
    const granted = await handler(request("/api/testers", "POST", { userId: id.toUpperCase(), label: "Impl", reason: "첫 베타" }));
    expect(granted.status).toBe(201);
    expect((await granted.json()).userId).toBe(id);
    const list = await (await handler(request("/api/testers"))).json();
    expect(list.testers).toHaveLength(1);
    expect(list.events[0]).toMatchObject({ action: "grant", actor: "operator", reason: "첫 베타" });
    const revoked = await handler(request(`/api/testers/${id}/revoke`, "POST", { reason: "테스트 종료" }));
    expect(revoked.status).toBe(200);
    expect((await revoked.json()).active).toBe(false);
    expect(repository.events.at(-1)).toMatchObject({ action: "revoke", reason: "테스트 종료" });
    expect((await handler(request(`/api/testers/${id}/revoke`, "POST", { reason: "중복" }))).status).toBe(404);
  });

  test("looks up a player and grants only the confirmed linked account", async () => {
    const repository = fakeRepository();
    let linkedUserId = id;
    const handler = createAdminHandler(repository, credentials, { html: "", css: "", javascript: "" }, async (playerId) => ({
      playerId,
      playerName: "Impl",
      username: "impl.dev",
      userId: linkedUserId,
    }));
    expect((await handler(new Request("http://127.0.0.1:4177/api/players/7410"))).status).toBe(401);
    expect((await handler(request("/api/players/0"))).status).toBe(400);
    expect(await (await handler(request("/api/players/7410"))).json()).toMatchObject({ userId: id, username: "impl.dev" });
    expect((await handler(request("/api/testers/by-player", "POST", {
      playerId: 7410, expectedUserId: id, reason: "첫 베타", label: "",
    }, { Origin: "http://evil.test" }))).status).toBe(403);
    linkedUserId = "11111111-1111-4111-8111-111111111111";
    expect((await handler(request("/api/testers/by-player", "POST", {
      playerId: 7410, expectedUserId: id, reason: "첫 베타", label: "",
    }))).status).toBe(409);
    expect(repository.testers).toHaveLength(0);
    const granted = await handler(request("/api/testers/by-player", "POST", {
      playerId: 7410, expectedUserId: linkedUserId, reason: "첫 베타", label: "",
    }));
    expect(granted.status).toBe(201);
    expect(await granted.json()).toMatchObject({ userId: linkedUserId, label: "impl.dev" });
  });

  test("rejects a public response without a linked account or with mismatched IDs", async () => {
    const fetchImpl = async () => new Response(JSON.stringify({ id: 7410, name: "Impl" }), { status: 200 });
    await expect(lookupLinkedPlayer(7410, "https://api.tuforums.com", fetchImpl)).rejects.toMatchObject({ status: 404 });
    const mismatchFetch = async () => new Response(JSON.stringify({
      id: 7410,
      user: { id, username: "impl.dev", playerId: 9999 },
    }), { status: 200 });
    await expect(lookupLinkedPlayer(7410, "https://api.tuforums.com", mismatchFetch)).rejects.toMatchObject({ status: 502 });
    const validFetch = async (url: RequestInfo | URL, init?: RequestInit) => {
      expect(String(url)).toBe("https://api.tuforums.com/v2/database/players/7410");
      expect(init?.redirect).toBe("error");
      expect(new Headers(init?.headers).has("Authorization")).toBe(false);
      return new Response(JSON.stringify({ id: 7410, name: "Impl", user: { id, username: "impl.dev", playerId: 7410 } }), { status: 200 });
    };
    expect(await lookupLinkedPlayer(7410, "https://api.tuforums.com", validFetch)).toMatchObject({ userId: id, playerId: 7410 });
  });
});
