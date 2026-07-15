import { Button } from "@/ui/button.component";
import { cn } from "@/ui/ui-class.utils";
import type { ConnectionStatus } from "../activity.model";

export function DashboardHeader({
  status,
  onRetry,
}: {
  status: ConnectionStatus;
  onRetry: () => void;
}) {
  return (
    <header className="flex min-h-14 items-center justify-between gap-3 border-b border-border bg-background px-4 py-2">
      <h1 className="font-heading text-2xl font-semibold tracking-tight">TUFReplay</h1>
      <div className="flex items-center gap-2">
        <span
          role="status"
          aria-label={status}
          title={status}
          className={cn(
            "size-2 rounded-full bg-muted-foreground",
            status === "online" && "bg-[#7DCF00]",
            status === "error" && "bg-destructive",
          )}
        />
        {status === "error" ? (
          <Button size="sm" onClick={onRetry}>
            Retry
          </Button>
        ) : null}
      </div>
    </header>
  );
}
