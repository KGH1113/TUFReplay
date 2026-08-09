import i18n from "../../i18n/i18n";
import { ActivityDomainError } from "../data/activity.gateway";

export function localizedErrorMessage(cause: unknown, fallback: string) {
  if (cause instanceof ActivityDomainError) {
    const translated = translatedDomainError(cause.code);
    if (translated) return translated;
  }
  const ipcMessage = translatedIpcError(cause);
  if (ipcMessage) return ipcMessage;
  return cause instanceof Error && cause.message ? cause.message : fallback;
}

function translatedIpcError(cause: unknown) {
  const code =
    cause && typeof cause === "object" && "code" in cause
      ? (cause as { code?: unknown }).code
      : null;
  if (code === "UNAVAILABLE") return i18n.t("errors.ipcUnavailable", { ns: "activity" });
  if (code === "TIMEOUT") return i18n.t("errors.ipcTimeout", { ns: "activity" });
  if (code === "VERSION_MISMATCH")
    return i18n.t("errors.ipcVersionMismatch", { ns: "activity" });
  if (code === "namespace_not_found") return i18n.t("errors.namespaceNotFound", { ns: "activity" });
  if (code === "namespace_initializing")
    return i18n.t("errors.namespaceInitializing", { ns: "activity" });
  if (code === "namespace_error") return i18n.t("errors.namespaceError", { ns: "activity" });
  if (code === "namespace_status_unavailable")
    return i18n.t("errors.ipcUpdateRequired", { ns: "activity" });
  return null;
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
