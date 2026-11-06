"use client";

import { Progress as ProgressPrimitive } from "radix-ui";
import type * as React from "react";
import { cn } from "@/shared/lib/cn";

function Progress({
  className,
  value,
  ...props
}: React.ComponentProps<typeof ProgressPrimitive.Root>) {
  return (
    <ProgressPrimitive.Root
      data-slot="progress"
      value={value}
      className={cn(
        "relative flex h-3 w-full items-center overflow-x-hidden rounded-4xl bg-muted",
        className,
      )}
      {...props}
    >
      <ProgressPrimitive.Indicator
        data-slot="progress-indicator"
        className={cn(
          "size-full flex-1 bg-primary transition-all motion-reduce:transition-none",
          value == null && "motion-safe:animate-pulse",
        )}
        style={{ transform: `translateX(-${value == null ? 60 : 100 - value}%)` }}
      />
    </ProgressPrimitive.Root>
  );
}

export { Progress };
