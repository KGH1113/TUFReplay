import type { LevelMetadata } from "@/models/activity/activity-model";
import { cn } from "@/shared/lib/cn";

/** TUF difficulty art, or a question mark when the difficulty is unknown or the level is local. */
export function LevelDifficultyBadge({
  metadata,
  label,
  size,
}: {
  metadata: LevelMetadata;
  label: string;
  size: "sm" | "lg";
}) {
  const box = size === "lg" ? "size-12" : "size-6";
  if (metadata.difficultyIconUrl)
    return (
      <img
        src={metadata.difficultyIconUrl}
        alt={label}
        className={cn(box, "shrink-0 object-contain drop-shadow-[0_2px_4px_rgb(0_0_0/0.6)]")}
        loading="lazy"
        decoding="async"
        draggable={false}
      />
    );
  return (
    <span
      role="img"
      aria-label={label}
      className={cn(
        box,
        "grid shrink-0 place-items-center rounded-full bg-foreground/[0.07] font-semibold text-muted-foreground ring-1 ring-foreground/10",
        size === "lg" ? "text-lg" : "text-xs",
      )}
    >
      <span aria-hidden="true">?</span>
    </span>
  );
}
