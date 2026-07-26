import type { ConnectionStatus } from "../activity.model";

interface ConnectionStatePanelCopy {
  title: string;
  description: string;
  retryLabel: string;
  guideLabel: string;
  steps: string[];
}

export function getConnectionStatePanelCopy(status: ConnectionStatus): ConnectionStatePanelCopy {
  if (status === "connecting")
    return {
      title: "Connecting to TUFReplay…",
      description: "Checking the local ADOFAI connection.",
      retryLabel: "Retry connection",
      guideLabel: "How to connect",
      steps: [
        "Start ADOFAI.",
        "Enable TUFReplay in UnityModManager.",
        "Make sure AdofaiIpc is running.",
      ],
    };

  if (status === "incompatible")
    return {
      title: "TUFReplay update required",
      description: "Fully quit ADOFAI, then start it again to update TUFReplay.",
      retryLabel: "Retry after restarting",
      guideLabel: "How to update",
      steps: [
        "Fully quit ADOFAI.",
        "Start ADOFAI again and wait for TUFReplay to update.",
        "Return here; this page will retry automatically.",
      ],
    };

  return {
    title: "Waiting for TUFReplay",
    description: "Open ADOFAI and make sure the mod is enabled.",
    retryLabel: "Retry connection",
    guideLabel: "How to connect",
    steps: [
      "Start ADOFAI.",
      "Enable TUFReplay in UnityModManager.",
      "Make sure AdofaiIpc is running.",
    ],
  };
}
