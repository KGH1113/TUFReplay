import i18n from "@/i18n/i18n";
import { ApiError } from "@/shared/errors/api-error";

export function localizedErrorMessage(cause: unknown, fallback: string) {
  const code = errorCode(cause);
  if (cause instanceof ApiError && cause.kind === "domain") {
    const translated = translatedDomainError(code);
    if (translated) return translated;
  }
  const ipcMessage = translatedIpcError(code);
  if (ipcMessage) return ipcMessage;
  return fallback;
}

export function diagnosticErrorMessage(cause: unknown) {
  if (cause instanceof Error && cause.message) {
    const nested =
      cause.cause instanceof Error && cause.cause.message ? ` (${cause.cause.message})` : "";
    return `${cause.message}${nested}`;
  }
  return typeof cause === "string" ? cause : "";
}

function errorCode(cause: unknown) {
  if (!cause || typeof cause !== "object" || !("code" in cause)) return "";
  const code = (cause as { code?: unknown }).code;
  return typeof code === "string" ? code : "";
}

function translatedIpcError(code: string) {
  if (code === "UNAVAILABLE" || code === "ipc_unavailable")
    return i18n.t("errors.ipcUnavailable", { ns: "activity" });
  if (code === "TIMEOUT" || code === "ipc_timeout")
    return i18n.t("errors.ipcTimeout", { ns: "activity" });
  if (code === "VERSION_MISMATCH" || code === "ipc_version_mismatch")
    return i18n.t("errors.ipcVersionMismatch", { ns: "activity" });
  if (code === "ipc_request_failed") return i18n.t("errors.ipcRequestFailed", { ns: "activity" });
  if (code === "namespace_not_found") return i18n.t("errors.namespaceNotFound", { ns: "activity" });
  if (code === "namespace_initializing")
    return i18n.t("errors.namespaceInitializing", { ns: "activity" });
  if (code === "namespace_error") return i18n.t("errors.namespaceError", { ns: "activity" });
  if (code === "namespace_status_unavailable")
    return i18n.t("errors.ipcUpdateRequired", { ns: "activity" });
  return null;
}

export function translatedDomainError(code: string) {
  const namespace = domainErrorNamespace(code);
  if (namespace && i18n.exists(`errors.${code}`, { ns: namespace })) {
    const translate = i18n.getFixedT(null, namespace);
    return translate(`errors.${code}` as never);
  }
  return null;
}

function domainErrorNamespace(code: string): "activity" | "microphone" | "replay" | null {
  if (
    code.startsWith("microphone_") ||
    code.startsWith("calibration_") ||
    code === "gameplay_active"
  )
    return "microphone";
  if (
    code.startsWith("level_session_") ||
    code.startsWith("logical_level_") ||
    code.startsWith("chart_") ||
    code.startsWith("activity_") ||
    code === "run_in_use" ||
    code === "run_delete_failed"
  )
    return "activity";
  return code ? "replay" : null;
}
