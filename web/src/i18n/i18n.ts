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
import downloadsEn from "./locales/en/downloads.json";
import microphoneEn from "./locales/en/microphone.json";
import renderEn from "./locales/en/render.json";
import replayEn from "./locales/en/replay.json";
import webcamEn from "./locales/en/webcam.json";
import activityKo from "./locales/ko/activity.json";
import commonKo from "./locales/ko/common.json";
import downloadsKo from "./locales/ko/downloads.json";
import microphoneKo from "./locales/ko/microphone.json";
import renderKo from "./locales/ko/render.json";
import replayKo from "./locales/ko/replay.json";
import webcamKo from "./locales/ko/webcam.json";

export const namespaces = [
  "common",
  "activity",
  "microphone",
  "replay",
  "webcam",
  "render",
  "downloads",
] as const;

let initialization: Promise<void> | null = null;

export function initializeI18n() {
  initialization ??= i18n
    .use(initReactI18next)
    .init({
      resources: {
        en: {
          common: commonEn,
          activity: activityEn,
          microphone: microphoneEn,
          replay: replayEn,
          render: renderEn,
          webcam: webcamEn,
          downloads: downloadsEn,
        },
        ko: {
          common: commonKo,
          activity: activityKo,
          microphone: microphoneKo,
          replay: replayKo,
          render: renderKo,
          webcam: webcamKo,
          downloads: downloadsKo,
        },
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
