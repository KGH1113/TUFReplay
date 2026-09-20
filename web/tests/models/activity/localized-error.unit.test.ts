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
    ).toBe("선택한 플레이를 찾을 수 없어요. 활동 목록을 새로고침해 주세요.");
  });

  test("keeps unknown diagnostics out of user-facing copy", () => {
    expect(localizedErrorMessage(new Error("bridge failed at frame 7"), "fallback")).toBe(
      "fallback",
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
    ).toBe("현재 플레이를 종료한 뒤 마이크 타이밍을 바꿀 수 있어요.");
  });

  test("translates typed IPC readiness and timeout errors", async () => {
    await i18n.changeLanguage("ko");
    expect(localizedErrorMessage({ code: "namespace_initializing" }, "fallback")).toBe(
      "TUFReplay가 시작 중이에요. 준비되면 자동으로 다시 연결할게요.",
    );
    expect(localizedErrorMessage({ code: "TIMEOUT" }, "fallback")).toContain(
      "30초 안에 응답하지 않았어요",
    );
  });
});
