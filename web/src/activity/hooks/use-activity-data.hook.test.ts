import { describe, expect, test } from "bun:test";

import { ActivityProtocolMismatchError } from "../data/activity.gateway";
import { connectionStatusForError } from "./use-activity-data.hook";

describe("activity connection errors", () => {
  test("maps protocol mismatches to the update-required state", () => {
    const error = new ActivityProtocolMismatchError(1, 2, "0.2.0");

    expect(connectionStatusForError(error)).toBe("incompatible");
  });

  test("keeps ordinary IPC failures in the offline error state", () => {
    expect(connectionStatusForError(new Error("Connection refused"))).toBe("error");
  });
});
