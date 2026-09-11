import "i18next";

import type activity from "./locales/en/activity.json";
import type common from "./locales/en/common.json";
import type microphone from "./locales/en/microphone.json";
import type replay from "./locales/en/replay.json";
import type submission from "./locales/en/submission.json";

declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "common";
    resources: {
      common: typeof common;
      activity: typeof activity;
      microphone: typeof microphone;
      replay: typeof replay;
      submission: typeof submission;
    };
  }
}
