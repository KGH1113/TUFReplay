import { cn } from "@/ui/ui-class.utils";

export function ScrollHint({ direction, visible }: { direction: "left" | "right" | "up" | "down"; visible: boolean }) {
  if (!visible) return null;

  return (
    <div
      className={cn(
        "pointer-events-none absolute z-30 flex items-center justify-center",
        direction === "left" && "inset-y-0 left-0 w-16 bg-gradient-to-r from-background via-background/80 to-transparent",
        direction === "right" && "inset-y-0 right-0 w-16 bg-gradient-to-l from-background via-background/80 to-transparent",
        direction === "up" && "inset-x-0 top-0 h-14 bg-gradient-to-b from-background via-background/80 to-transparent",
        direction === "down" && "inset-x-0 bottom-0 h-14 bg-gradient-to-t from-background via-background/80 to-transparent",
      )}
    >
      <span
        className={cn(
          "block size-4 border-r-2 border-t-2 border-primary",
          direction === "right" && "rotate-45",
          direction === "down" && "rotate-[135deg]",
          direction === "left" && "rotate-[225deg]",
          direction === "up" && "-rotate-45",
        )}
      />
    </div>
  );
}

