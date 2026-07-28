import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import {
  detectInitialLanguage,
  normalizeLanguage,
  persistLanguage,
  type SupportedLanguage,
} from "./language";
import activityEn from "./locales/en/activity.json";
import commonEn from "./locales/en/common.json";
import microphoneEn from "./locales/en/microphone.json";
import replayEn from "./locales/en/replay.json";
import activityKo from "./locales/ko/activity.json";
import commonKo from "./locales/ko/common.json";
import microphoneKo from "./locales/ko/microphone.json";
import replayKo from "./locales/ko/replay.json";

export const namespaces = ["common", "activity", "microphone", "replay"] as const;

let initialization: Promise<void> | null = null;

export function initializeI18n() {
  initialization ??= i18n
    .use(initReactI18next)
    .init({
      resources: {
        en: { common: commonEn, activity: activityEn, microphone: microphoneEn, replay: replayEn },
        ko: { common: commonKo, activity: activityKo, microphone: microphoneKo, replay: replayKo },
      },
      lng: detectInitialLanguage(),
      fallbackLng: "en",
      supportedLngs: ["en", "ko"],
      defaultNS: "common",
      ns: namespaces,
      interpolation: { escapeValue: false },
      returnNull: false,
      showSupportNotice: false,
    })
    .then(() => applyDocumentLanguage(normalizeLanguage(i18n.resolvedLanguage)));
  return initialization;
}

export async function setAppLanguage(language: SupportedLanguage) {
  await initializeI18n();
  persistLanguage(language);
  await i18n.changeLanguage(language);
}

i18n.on("languageChanged", (language) => applyDocumentLanguage(normalizeLanguage(language)));

function applyDocumentLanguage(language: SupportedLanguage) {
  if (typeof document !== "undefined") document.documentElement.lang = language;
}

export default i18n;
