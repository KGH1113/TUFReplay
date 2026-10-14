import { useRef, useState } from "react";
import type { ActivityRun } from "@/models/activity/activity-model";

export function useRenderComingSoon() {
  const [open, setOpen] = useState(false);
  const returnFocus = useRef<HTMLElement | null>(null);

  return {
    open,
    setOpen,
    show: (_run: ActivityRun, trigger?: HTMLElement) => {
      returnFocus.current = trigger ?? null;
      setOpen(true);
    },
    restoreFocus: () => {
      if (returnFocus.current?.isConnected) returnFocus.current.focus();
    },
  };
}
