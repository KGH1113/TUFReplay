import { describe, expect, test } from "bun:test";
import { awaitDomainEvent } from "@/application/command-events";
import type { DomainMessage, LocalMessagePeer } from "@/ports/local-message-peer";

function fixture() {
  let nextId = 0;
  const listeners = new Map<string, Set<(value: unknown, message: DomainMessage) => void>>();
  const sent: Array<{ id: string; name: string; payload: object }> = [];
  const peer: LocalMessagePeer = {
    whenReady: async () => {},
    send(name, payload = {}) {
      const id = `command-${++nextId}`;
      sent.push({ id, name, payload });
      return id;
    },
    on(name, listener) {
      const set = listeners.get(name) ?? new Set();
      set.add(listener);
      listeners.set(name, set);
      return () => {
        set.delete(listener);
      };
    },
  };
  return {
    peer,
    sent,
    listenerCount: () => [...listeners.values()].reduce((total, set) => total + set.size, 0),
    emit(name: string, correlationId: string, payload: unknown) {
      for (const listener of listeners.get(name) ?? [])
        listener(payload, { id: "event", correlationId, payload });
    },
  };
}

describe("domain command outcomes", () => {
  test("waits for the matching named result and releases subscriptions", async () => {
    const f = fixture();
    const result = awaitDomainEvent(
      f.peer,
      "replay.start",
      "replay.state.changed",
      { runId: "one" },
      String,
    );
    f.emit("replay.state.changed", "unrelated", "wrong");
    f.emit("command.accepted", "command-1", {});
    expect(f.listenerCount()).toBe(3);
    f.emit("replay.state.changed", "command-1", "playing");
    expect(await result).toBe("playing");
    expect(f.listenerCount()).toBe(0);
    expect(f.sent).toEqual([{ id: "command-1", name: "replay.start", payload: { runId: "one" } }]);
  });
  test("rejects a correlated domain rejection immediately", async () => {
    const f = fixture();
    const result = awaitDomainEvent(f.peer, "replay.start", "replay.state.changed", {}, String);
    f.emit("command.rejected", "command-1", {
      code: "replay_busy",
      message: "Finish the active replay.",
    });
    await expect(result).rejects.toMatchObject({ code: "replay_busy" });
    expect(f.listenerCount()).toBe(0);
  });
  test("cancellation drops stale results and releases every listener", async () => {
    const f = fixture();
    const stop = new AbortController();
    const result = awaitDomainEvent(
      f.peer,
      "calibration.start",
      "calibration.state.changed",
      {},
      String,
      { signal: stop.signal },
    );
    stop.abort();
    await expect(result).rejects.toMatchObject({ name: "AbortError" });
    f.emit("calibration.state.changed", "command-1", "late");
    expect(f.listenerCount()).toBe(0);
  });
  test("a lost namespace fails the pending action and releases status subscriptions", async () => {
    const f = fixture();
    let changed: ((status: "ready" | "unavailable") => void) | undefined;
    let statusListeners = 0;
    f.peer.onStatus = (listener) => {
      changed = listener;
      statusListeners++;
      listener("ready");
      return () => {
        statusListeners--;
      };
    };
    const result = awaitDomainEvent(f.peer, "replay.start", "replay.state.changed", {}, String);
    changed?.("unavailable");
    await expect(result).rejects.toMatchObject({ code: "ipc_unavailable" });
    expect(f.listenerCount()).toBe(0);
    expect(statusListeners).toBe(0);
  });
});
