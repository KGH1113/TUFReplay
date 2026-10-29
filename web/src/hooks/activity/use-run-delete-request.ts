import { useRef, useState } from "react";
import type { ActivityRun } from "@/models/activity/activity-model";
import { localizedErrorMessage } from "@/models/activity/localized-error";

/** One delete confirmation for the whole page, opened from a row menu or the run inspector. */
export function useRunDeleteRequest(
  onDeleteRun: (run: ActivityRun) => Promise<void>,
  failureMessage: string,
) {
  const [run, setRun] = useState<ActivityRun | null>(null);
  const [open, setOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");
  const returnFocus = useRef<HTMLElement | null>(null);

  return {
    run,
    open,
    deleting,
    error,
    request: (target: ActivityRun) => {
      if (target.locked) return;
      returnFocus.current =
        document.activeElement instanceof HTMLElement ? document.activeElement : null;
      setRun(target);
      setError("");
      setOpen(true);
    },
    setOpen: (next: boolean) => {
      if (!deleting) setOpen(next);
    },
    confirm: async () => {
      if (!run || deleting) return;
      setDeleting(true);
      setError("");
      try {
        await onDeleteRun(run);
        setOpen(false);
      } catch (cause) {
        setError(localizedErrorMessage(cause, failureMessage));
      } finally {
        setDeleting(false);
      }
    },
    restoreFocus: () => {
      const target = returnFocus.current;
      returnFocus.current = null;
      if (target?.isConnected) target.focus({ preventScroll: true });
    },
  };
}
