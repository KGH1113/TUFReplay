import { useTranslation } from "react-i18next";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";

/** One plain line under the app name saying whether ADOFAI is reachable. */
export function ConnectionStatusText({
  status,
  mockEnabled,
}: {
  status: ConnectionStatus;
  mockEnabled: boolean;
}) {
  const { t } = useTranslation("common");
  const problem = status === "error" || status === "incompatible";
  return (
    <p
      role="status"
      className={cn(
        "truncate text-xs leading-tight",
        problem ? "text-warning" : "text-muted-foreground",
      )}
    >
      {mockEnabled
        ? t("connection.demo")
        : t(
            status === "online"
              ? "connection.connected"
              : status === "connecting"
                ? "connection.connecting"
                : status === "incompatible"
                  ? "connection.updateRequired"
                  : "connection.disconnected",
          )}
    </p>
  );
}

/** Retry sits next to the status only while the connection has failed. */
export function ConnectionRetryButton({
  status,
  onRetry,
}: {
  status: ConnectionStatus;
  onRetry: () => void;
}) {
  const { t } = useTranslation("common");
  if (status !== "error" && status !== "incompatible") return null;
  return (
    <Button size="xs" variant="secondary" onClick={onRetry} className="shrink-0">
      {t("actions.retry")}
    </Button>
  );
}
