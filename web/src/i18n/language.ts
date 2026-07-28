export const LANGUAGE_STORAGE_KEY = "tuf-replay.language";

export type SupportedLanguage = "en" | "ko";

export function normalizeLanguage(language: string | null | undefined): SupportedLanguage {
  const normalized = language?.trim().toLowerCase();
  return normalized === "ko" || normalized?.startsWith("ko-") ? "ko" : "en";
}

export function detectInitialLanguage(
  storage: Pick<Storage, "getItem"> | null = readableLocalStorage(),
  browserLanguages: readonly string[] = readableBrowserLanguages(),
): SupportedLanguage {
  try {
    const stored = storage?.getItem(LANGUAGE_STORAGE_KEY);
    if (stored === "en" || stored === "ko") return stored;
  } catch {
    // Storage can be unavailable in privacy-restricted browser contexts.
  }

  for (const language of browserLanguages) {
    const supported = supportedLanguage(language);
    if (supported) return supported;
  }
  return "en";
}

export function persistLanguage(
  language: SupportedLanguage,
  storage: Pick<Storage, "setItem"> | null = writableLocalStorage(),
) {
  try {
    storage?.setItem(LANGUAGE_STORAGE_KEY, language);
  } catch {
    // The language still changes for this page when storage is unavailable.
  }
}

function readableLocalStorage() {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

function writableLocalStorage() {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

function readableBrowserLanguages() {
  try {
    return typeof navigator === "undefined" ? [] : (navigator.languages ?? []);
  } catch {
    return [];
  }
}

function supportedLanguage(language: string | null | undefined): SupportedLanguage | null {
  const normalized = language?.trim().toLowerCase();
  if (normalized === "ko" || normalized?.startsWith("ko-")) return "ko";
  if (normalized === "en" || normalized?.startsWith("en-")) return "en";
  return null;
}
