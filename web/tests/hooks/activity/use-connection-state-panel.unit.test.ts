import { beforeAll, describe, expect, test } from "bun:test";
import { getConnectionStatePanelCopy } from "@/hooks/activity/use-connection-state-panel";
import i18n, { initializeI18n } from "@/i18n/i18n";

describe("connection state panel copy", () => {
  beforeAll(initializeI18n);

  test("shows restart instructions for an incompatible mod", () => {
    const copy = getConnectionStatePanelCopy("incompatible", i18n.getFixedT("en", "activity"));

    expect(copy.title).toBe("TUFReplay update required");
    expect(copy.description).toContain("Fully quit ADOFAI");
    expect(copy.retryLabel).toBe("Retry after restarting");
  });

  test("keeps ordinary connection failures on the existing offline guidance", () => {
    const copy = getConnectionStatePanelCopy("error", i18n.getFixedT("en", "activity"));

    expect(copy.title).toBe("Waiting for TUFReplay");
    expect(copy.retryLabel).toBe("Retry connection");
  });

  test("asks for a TUFReplay update when the game side is older, without naming its library", () => {
    for (const direction of ["server_outdated", "legacy_server"] as const) {
      const copy = getConnectionStatePanelCopy(
        "incompatible",
        i18n.getFixedT("en", "activity"),
        direction,
      );
      expect(copy.title).toBe("TUFReplay update required");
      expect(copy.action).toBe("retry");
      expect(JSON.stringify(copy)).not.toMatch(/ipc/i);
    }
  });
});
