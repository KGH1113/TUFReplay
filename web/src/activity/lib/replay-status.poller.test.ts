import { describe, expect, test } from "bun:test";

import type { ReplayStatus } from "../activity.model";
import { shouldPollReplayStatus } from "../hooks/use-replay-control.hook";
import { ReplayStatusPoller } from "./replay-status.poller";

const preparing: ReplayStatus = {
  OperationId: "op-1",
  RunId: "run-1",
  State: "preparing",
  ErrorCode: null,
  Message: null,
};

describe("replay status polling", () => {
  test("coalesces overlapping refreshes", async () => {
    let resolve!: (status: ReplayStatus) => void;
    let calls = 0;
    const statuses: ReplayStatus[] = [];
    const poller = new ReplayStatusPoller(
      () => {
        calls += 1;
        return new Promise((next) => {
          resolve = next;
        });
      },
      (status) => statuses.push(status),
      () => {},
    );

    const first = poller.refresh();
    await poller.refresh();
    expect(calls).toBe(1);
    resolve(preparing);
    await first;
    expect(statuses).toEqual([preparing]);
  });

  test("ignores a response invalidated by a newer play request", async () => {
    let resolve!: (status: ReplayStatus) => void;
    const statuses: ReplayStatus[] = [];
    const poller = new ReplayStatusPoller(
      () =>
        new Promise((next) => {
          resolve = next;
        }),
      (status) => statuses.push(status),
      () => {},
    );

    const request = poller.refresh();
    poller.invalidate();
    resolve(preparing);
    await request;
    expect(statuses).toEqual([]);
  });

  test("polls only non-terminal replay states", () => {
    expect(shouldPollReplayStatus(preparing)).toBe(true);
    expect(shouldPollReplayStatus({ ...preparing, State: "playing" })).toBe(true);
    expect(shouldPollReplayStatus({ ...preparing, State: "completed" })).toBe(false);
    expect(shouldPollReplayStatus({ ...preparing, State: "cancelled" })).toBe(false);
    expect(shouldPollReplayStatus({ ...preparing, State: "error" })).toBe(false);
  });
});
