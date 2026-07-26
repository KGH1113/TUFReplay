import { describe, expect, test } from "bun:test";

import { getConnectionStatePanelCopy } from "./connection-state-panel.copy";

describe("connection state panel copy", () => {
  test("shows restart instructions for an incompatible mod", () => {
    const copy = getConnectionStatePanelCopy("incompatible");

    expect(copy.title).toBe("TUFReplay update required");
    expect(copy.description).toContain("Fully quit ADOFAI");
    expect(copy.retryLabel).toBe("Retry after restarting");
  });

  test("keeps ordinary connection failures on the existing offline guidance", () => {
    const copy = getConnectionStatePanelCopy("error");

    expect(copy.title).toBe("Waiting for TUFReplay");
    expect(copy.retryLabel).toBe("Retry connection");
  });
});
