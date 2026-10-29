import { SquareLock02Icon, SquareUnlock02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import { cn } from "@/shared/lib/cn";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

/** Hidden until the card is hovered or focused, and always shown while the run is locked. */
export function RunLockButton({
  run,
  disabled,
  onSetLocked,
}: {
  run: ActivityRun;
  disabled: boolean;
  onSetLocked: (run: ActivityRun, locked: boolean) => Promise<void>;
}) {
  const { t } = useTranslation("activity");
  // Flip the icon on press; the recorder confirms right after.
  const [optimistic, setOptimistic] = useState<boolean | null>(null);
  const locked = optimistic ?? run.locked;
  const toggle = async () => {
    if (disabled || optimistic !== null) return;
    const next = !locked;
    setOptimistic(next);
    try {
      await onSetLocked(run, next);
    } catch (cause) {
      toast.error(localizedErrorMessage(cause, t("lock.failed")));
    } finally {
      setOptimistic(null);
    }
  };

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type="button"
          aria-pressed={locked}
          aria-label={t(locked ? "lock.toggleUnlock" : "lock.toggleLock", {
            runIndex: run.runIndex,
          })}
          disabled={disabled}
          onClick={() => void toggle()}
          className={cn(
            "grid size-6 shrink-0 place-items-center rounded-md transition-[opacity,background-color,color,transform] duration-150 active:scale-90 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed motion-reduce:active:scale-100",
            locked
              ? "bg-foreground/10 text-foreground hover:bg-foreground/15"
              : "text-muted-foreground opacity-0 hover:bg-foreground/8 hover:text-foreground group-hover/run:opacity-100 group-focus-within/run:opacity-100 [@media(hover:none)]:opacity-100",
          )}
        >
          <HugeiconsIcon
            aria-hidden="true"
            icon={locked ? SquareLock02Icon : SquareUnlock02Icon}
            size={15}
            strokeWidth={2}
          />
        </button>
      </TooltipTrigger>
      <TooltipContent side="top">
        {t(locked ? "lock.lockedTooltip" : "lock.unlockedTooltip")}
      </TooltipContent>
    </Tooltip>
  );
}
