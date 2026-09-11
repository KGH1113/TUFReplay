import { beforeAll, describe, expect, test } from "bun:test";

import i18n, { initializeI18n } from "@/i18n/i18n";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import { ApiError } from "@/shared/errors/api-error";

describe("localized errors", () => {
  beforeAll(initializeI18n);

  test("translates a known domain error code", async () => {
    await i18n.changeLanguage("ko");
    expect(
      localizedErrorMessage(
        new ApiError("Run was not found", { kind: "domain", code: "run_not_found" }),
        "fallback",
      ),
    ).toBe("선택한 플레이를 찾을 수 없습니다.");
  });

  test("preserves an unknown diagnostic message", () => {
    expect(localizedErrorMessage(new Error("bridge failed at frame 7"), "fallback")).toBe(
      "bridge failed at frame 7",
    );
  });

  test("translates locked microphone timing adjustments", async () => {
    await i18n.changeLanguage("ko");
    expect(
      localizedErrorMessage(
        new ApiError("Timing is locked", {
          kind: "domain",
          code: "microphone_timing_locked",
        }),
        "fallback",
      ),
    ).toBe("현재 플레이를 먼저 종료하세요");
  });

  test("translates typed IPC readiness and timeout errors", async () => {
    await i18n.changeLanguage("ko");
    expect(localizedErrorMessage({ code: "namespace_initializing" }, "fallback")).toBe(
      "TUFReplay가 초기화 중입니다. 잠시 후 다시 시도하세요.",
    );
    expect(localizedErrorMessage({ code: "TIMEOUT" }, "fallback")).toBe(
      "TUFReplay 응답 시간이 너무 오래 걸렸습니다.",
    );
  });
});
