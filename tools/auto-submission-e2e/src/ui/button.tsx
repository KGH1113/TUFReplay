import type { ComponentProps } from "react";
import { Slot } from "radix-ui";
import { cva, type VariantProps } from "class-variance-authority";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

// shadcn Button composition, styled for the local test workspace.
const variants = cva(
  "inline-flex items-center justify-center gap-2 rounded-md px-4 h-10 text-sm font-medium transition-colors disabled:opacity-40 disabled:pointer-events-none focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-emerald-300",
  {
    variants: {
      variant: {
        default: "bg-emerald-300 text-zinc-950 hover:bg-emerald-200",
        secondary: "bg-zinc-800 text-zinc-100 hover:bg-zinc-700",
        ghost: "text-zinc-400 hover:bg-zinc-800 hover:text-zinc-100",
      },
    },
    defaultVariants: { variant: "default" },
  },
);
export function Button({
  variant,
  className,
  asChild = false,
  ...props
}: ComponentProps<"button"> & VariantProps<typeof variants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot.Root : "button";
  return (
    <Comp
      data-slot="button"
      className={twMerge(clsx(variants({ variant }), className))}
      {...props}
    />
  );
}
