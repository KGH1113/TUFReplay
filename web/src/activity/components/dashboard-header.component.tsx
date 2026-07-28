import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/ui/button.component";
import { cn } from "@/ui/ui-class.utils";
import type { ConnectionStatus } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { LanguageMenu } from "./language-menu.component";
import { MicrophoneControls } from "./microphone-controls.component";

export function DashboardHeader({
  status,
  onRetry,
  gatewayRef,
  mockEnabled,
}: {
  status: ConnectionStatus;
  onRetry: () => void;
  gatewayRef: RefObject<ActivityGateway | null>;
  mockEnabled: boolean;
}) {
  const { t } = useTranslation("common");
  return (
    <header className="flex min-h-14 items-center justify-between gap-3 border-b border-border bg-background px-4 py-2">
      <h1 className="font-heading text-2xl font-semibold tracking-tight">{t("appName")}</h1>
      <div className="flex items-center gap-2">
        <MicrophoneControls
          gatewayRef={gatewayRef}
          connectionStatus={status}
          mockEnabled={mockEnabled}
        />
        <span
          role="status"
          className={cn(
            "inline-flex h-7 items-center gap-1.5 rounded-full border px-2.5 text-[11px] font-medium",
            status === "online" && "border-primary/25 bg-primary/8 text-primary",
            status === "connecting" && "border-border bg-muted/40 text-muted-foreground",
            status === "error" && "border-amber-400/25 bg-amber-400/8 text-amber-300",
            status === "incompatible" && "border-amber-400/25 bg-amber-400/8 text-amber-300",
          )}
        >
          <span
            className={cn(
              "size-1.5 rounded-full bg-current",
              status === "connecting" && "animate-pulse",
            )}
          />
          {t(
            status === "online"
              ? "status.online"
              : status === "connecting"
                ? "status.connecting"
                : status === "incompatible"
                  ? "status.updateRequired"
                  : "status.offline",
          )}
        </span>
        {status === "error" || status === "incompatible" ? (
          <Button size="sm" variant="ghost" onClick={onRetry}>
            {t("actions.retryConnection")}
          </Button>
        ) : null}
        <LanguageMenu />
      </div>
    </header>
  );
}
