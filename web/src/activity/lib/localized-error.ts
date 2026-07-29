import i18n from "../../i18n/i18n";
import { ActivityDomainError } from "../data/activity.gateway";

export function localizedErrorMessage(cause: unknown, fallback: string) {
  if (cause instanceof ActivityDomainError) {
    const translated = translatedDomainError(cause.code);
    if (translated) return translated;
  }
  return cause instanceof Error && cause.message ? cause.message : fallback;
}

export function translatedDomainError(code: string) {
  if (code === "run_not_found") return i18n.t("errors.run_not_found", { ns: "replay" });
  if (code === "level_gameplay_modified")
    return i18n.t("errors.level_gameplay_modified", { ns: "replay" });
  if (code === "level_file_invalid") return i18n.t("errors.level_file_invalid", { ns: "replay" });
  if (code === "calibration_operation_stale")
    return i18n.t("errors.calibration_operation_stale", { ns: "replay" });
  return null;
}
