import type { IpcVersionMismatchDirection } from "@adofai-ipc/client";
import type { TFunction } from "i18next";
import type { ConnectionStatus } from "../activity.model";

interface ConnectionStatePanelCopy {
  title: string;
  description: string;
  retryLabel: string;
  guideLabel: string;
  steps: string[];
  action: "retry" | "download" | "reload";
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

  if (versionMismatch === "server_outdated" || versionMismatch === "legacy_server")
    return {
      title: t("connection.ipcUpdateTitle"),
      description: t("connection.ipcUpdateDescription"),
      retryLabel: t("connection.downloadIpc"),
      guideLabel: t("connection.ipcUpdateGuide"),
      steps: t("connection.ipcUpdateSteps", { returnObjects: true }),
      action: "download",
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

  if (status === "incompatible")
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
