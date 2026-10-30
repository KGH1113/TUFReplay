import { Loading03Icon, WifiDisconnected01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { Button } from "@/shared/ui/button";

/** Shown above saved history while the game is unreachable, so read-only controls make sense. */
export function OfflineBanner({
  status,
  onRetry,
}: {
  status: ConnectionStatus;
  onRetry: () => void;
}) {
  const { t } = useTranslation("activity");
  if (status === "online" || status === "incompatible") return null;
  const connecting = status === "connecting";
  return (
    <div
      role="status"
      aria-live="polite"
      className="flex shrink-0 items-center gap-3 border-b border-warning/20 bg-warning/8 px-5 py-2 text-xs"
    >
      <HugeiconsIcon
        aria-hidden="true"
        icon={connecting ? Loading03Icon : WifiDisconnected01Icon}
        size={15}
        strokeWidth={2}
        className={
          connecting
            ? "shrink-0 animate-spin text-muted-foreground motion-reduce:animate-none"
            : "shrink-0 text-warning"
        }
      />
      <p className="min-w-0 flex-1 leading-relaxed">
        <span className="font-semibold">
          {t(connecting ? "offline.reconnectingTitle" : "offline.title")}
        </span>{" "}
        <span className="text-muted-foreground">{t("offline.body")}</span>
      </p>
      {connecting ? null : (
        <Button size="xs" variant="secondary" onClick={onRetry}>
          {t("connection.retry")}
        </Button>
      )}
    </div>
  );
}
