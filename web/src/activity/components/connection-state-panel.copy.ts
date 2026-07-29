import type { TFunction } from "i18next";
import type { ConnectionStatus } from "../activity.model";

interface ConnectionStatePanelCopy {
  title: string;
  description: string;
  retryLabel: string;
  guideLabel: string;
  steps: string[];
}

export function getConnectionStatePanelCopy(
  status: ConnectionStatus,
  t: TFunction<"activity">,
): ConnectionStatePanelCopy {
  if (status === "connecting")
    return {
      title: t("connection.connectingTitle"),
      description: t("connection.connectingDescription"),
      retryLabel: t("connection.retry"),
      guideLabel: t("connection.connectGuide"),
      steps: t("connection.connectSteps", { returnObjects: true }),
    };

  if (status === "incompatible")
    return {
      title: t("connection.updateTitle"),
      description: t("connection.updateDescription"),
      retryLabel: t("connection.retryAfterRestarting"),
      guideLabel: t("connection.updateGuide"),
      steps: t("connection.updateSteps", { returnObjects: true }),
    };

  return {
    title: t("connection.waitingTitle"),
    description: t("connection.waitingDescription"),
    retryLabel: t("connection.retry"),
    guideLabel: t("connection.connectGuide"),
    steps: t("connection.connectSteps", { returnObjects: true }),
  };
}
