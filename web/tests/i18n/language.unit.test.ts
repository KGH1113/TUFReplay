import { describe, expect, test } from "bun:test";

import {
  detectInitialLanguage,
  LANGUAGE_STORAGE_KEY,
  normalizeLanguage,
  persistLanguage,
} from "@/i18n/language";

describe("application language", () => {
  test("normalizes Korean locales and falls back to English", () => {
    expect(normalizeLanguage("ko-KR")).toBe("ko");
    expect(normalizeLanguage("ko")).toBe("ko");
    expect(normalizeLanguage("en-US")).toBe("en");
    expect(normalizeLanguage(undefined)).toBe("en");
  });

  test("prefers a supported stored language over the browser language", () => {
    const storage = { getItem: (key: string) => (key === LANGUAGE_STORAGE_KEY ? "en" : null) };
    expect(detectInitialLanguage(storage, ["ko-KR"])).toBe("en");
  });

  test("uses the browser language when storage is empty or invalid", () => {
    expect(detectInitialLanguage({ getItem: () => null }, ["ko-KR", "en-US"])).toBe("ko");
    expect(detectInitialLanguage({ getItem: () => "ja" }, ["ja-JP"])).toBe("en");
    expect(detectInitialLanguage({ getItem: () => null }, ["ja-JP", "ko-KR"])).toBe("ko");
  });

  test("survives restricted storage", () => {
    const restricted = {
      getItem: () => {
        throw new Error("denied");
      },
      setItem: () => {
        throw new Error("denied");
      },
    };
    expect(detectInitialLanguage(restricted, ["ko-KR"])).toBe("ko");
    expect(() => persistLanguage("ko", restricted)).not.toThrow();
  });

  test("persists only the selected language value", () => {
    const values = new Map<string, string>();
    persistLanguage("ko", { setItem: (key, value) => values.set(key, value) });
    expect(values).toEqual(new Map([[LANGUAGE_STORAGE_KEY, "ko"]]));
  });
});
