import { beforeAll, describe, expect, test } from "bun:test";

import i18n, { initializeI18n } from "../../i18n/i18n";
import { ActivityDomainError } from "../data/activity.gateway";
import { localizedErrorMessage } from "./localized-error";

describe("localized errors", () => {
  beforeAll(initializeI18n);

  test("translates a known domain error code", async () => {
    await i18n.changeLanguage("ko");
    expect(
      localizedErrorMessage(
        new ActivityDomainError("run_not_found", "Run was not found"),
        "fallback",
      ),
    ).toBe("선택한 플레이를 찾을 수 없습니다.");
  });

  test("preserves an unknown diagnostic message", () => {
    expect(localizedErrorMessage(new Error("bridge failed at frame 7"), "fallback")).toBe(
      "bridge failed at frame 7",
    );
  });
});
