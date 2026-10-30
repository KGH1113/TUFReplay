import type { ArrowRight01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { ComponentProps } from "react";
import { cn } from "@/shared/lib/cn";

type IconData = typeof ArrowRight01Icon;

/** A labeled sidebar row that opens a setting; its second line says what the setting is doing. */
export function SidebarSettingButton({
  icon,
  label,
  value,
  attention = false,
  className,
  ...props
}: {
  icon: IconData;
  label: string;
  value?: string;
  /** The setting needs the player to act, such as approving a download or fixing a camera. */
  attention?: boolean;
} & ComponentProps<"button">) {
  return (
    <button
      type="button"
      {...props}
      className={cn(
        "group/setting flex min-h-11 w-full min-w-0 items-center gap-2.5 rounded-lg px-2.5 py-1.5 text-left text-sm transition-[background-color,color,transform] duration-150 hover:bg-sidebar-accent active:scale-[0.985] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring disabled:pointer-events-none disabled:opacity-45 data-[state=open]:bg-sidebar-accent motion-reduce:active:scale-100",
        className,
      )}
    >
      <HugeiconsIcon
        aria-hidden="true"
        icon={icon}
        size={17}
        strokeWidth={1.8}
        className="shrink-0 text-muted-foreground group-hover/setting:text-foreground"
      />
      <span className="min-w-0 flex-1">
        <span className="block truncate font-medium leading-tight">{label}</span>
        {value ? (
          <span
            className={cn(
              "block truncate text-xs leading-tight",
              attention ? "text-warning" : "text-muted-foreground",
            )}
          >
            {value}
          </span>
        ) : null}
      </span>
    </button>
  );
}
