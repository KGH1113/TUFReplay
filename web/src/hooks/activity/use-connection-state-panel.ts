import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import type { IpcVersionMismatchDirection } from "@/ports/local-message-peer";

interface ConnectionStatePanelCopy {
  title: string;
  description: string;
  retryLabel: string;
  guideLabel: string;
  steps: string[];
  action: "retry" | "reload";
}

interface UseConnectionStatePanelOptions {
  status: ConnectionStatus;
  error: string;
  onRetry: () => void;
  versionMismatch: IpcVersionMismatchDirection | null;
}

export function useConnectionStatePanelViewModel({
  status,
  error,
  onRetry,
  versionMismatch,
}: UseConnectionStatePanelOptions) {
  const { t: activityT } = useTranslation("activity");
  const { t: commonT } = useTranslation("common");
  const copy = getConnectionStatePanelCopy(status, activityT, versionMismatch);
  const primaryAction = {
    label: copy.retryLabel,
    onPress: copy.action === "reload" ? () => window.location.reload() : onRetry,
  };

  return {
    connecting: status === "connecting",
    title: copy.title,
    description: copy.description,
    guideLabel: copy.guideLabel,
    steps: copy.steps,
    diagnosticLabel: commonT("details"),
    diagnostic: error || commonT("errors.localEndpoint"),
    primaryAction,
  };
}

export function getConnectionStatePanelCopy(
  status: ConnectionStatus,
  t: TFunction<"activity">,
  versionMismatch: IpcVersionMismatchDirection | null = null,
): ConnectionStatePanelCopy {
  if (versionMismatch === "client_outdated")
    return {
      title: t("connection.clientUpdateTitle"),
      description: t("connection.clientUpdateDescription"),
      retryLabel: t("connection.reload"),
      guideLabel: t("connection.clientUpdateGuide"),
      steps: t("connection.clientUpdateSteps", { returnObjects: true }),
      action: "reload",
    };

  if (status === "connecting")
    return {
      title: t("connection.connectingTitle"),
      description: t("connection.connectingDescription"),
      retryLabel: t("connection.retry"),
      guideLabel: t("connection.connectGuide"),
      steps: t("connection.connectSteps", { returnObjects: true }),
      action: "retry",
    };

  // The connection library ships inside TUFReplay, so an older game side means an older mod.
  if (
    status === "incompatible" ||
    versionMismatch === "server_outdated" ||
    versionMismatch === "legacy_server"
  )
    return {
      title: t("connection.updateTitle"),
      description: t("connection.updateDescription"),
      retryLabel: t("connection.retryAfterRestarting"),
      guideLabel: t("connection.updateGuide"),
      steps: t("connection.updateSteps", { returnObjects: true }),
      action: "retry",
    };

  return {
    title: t("connection.waitingTitle"),
    description: t("connection.waitingDescription"),
    retryLabel: t("connection.retry"),
    guideLabel: t("connection.connectGuide"),
    steps: t("connection.connectSteps", { returnObjects: true }),
    action: "retry",
  };
}
