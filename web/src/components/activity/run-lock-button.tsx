import { SquareLock02Icon, SquareUnlock02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import { cn } from "@/shared/lib/cn";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

/** Quiet until the row is hovered, focused or selected; always shown while the run is locked. */
export function RunLockButton({
  run,
  disabled,
  revealed = false,
  onSetLocked,
}: {
  run: ActivityRun;
  disabled: boolean;
  revealed?: boolean;
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
          data-attempt-control
          aria-pressed={locked}
          aria-label={t(locked ? "lock.toggleUnlock" : "lock.toggleLock", {
            runIndex: run.runIndex,
          })}
          disabled={disabled}
          onClick={(event) => {
            event.stopPropagation();
            void toggle();
          }}
          onDoubleClick={(event) => event.stopPropagation()}
          className={cn(
            "grid size-7 shrink-0 place-items-center rounded-md transition-[opacity,background-color,color,transform] duration-150 active:scale-90 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed motion-reduce:active:scale-100",
            locked
              ? "text-primary hover:bg-primary/10"
              : cn(
                  "text-subtle-foreground hover:bg-foreground/8 hover:text-foreground group-hover/attempt:opacity-100 group-focus-within/attempt:opacity-100 [@media(hover:none)]:opacity-100",
                  revealed ? "opacity-100" : "opacity-0",
                ),
          )}
        >
          <HugeiconsIcon
            aria-hidden="true"
            icon={locked ? SquareLock02Icon : SquareUnlock02Icon}
            size={16}
            strokeWidth={locked ? 2.2 : 1.8}
          />
        </button>
      </TooltipTrigger>
      <TooltipContent side="top">
        {t(locked ? "lock.lockedTooltip" : "lock.unlockedTooltip")}
      </TooltipContent>
    </Tooltip>
  );
}
