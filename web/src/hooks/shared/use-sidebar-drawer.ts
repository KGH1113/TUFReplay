import { useCallback, useState } from "react";

/** Open state of the sidebar drawer used below the large breakpoint. */
export function useSidebarDrawer() {
  const [open, setOpen] = useState(false);
  return {
    open,
    setOpen,
    show: useCallback(() => setOpen(true), []),
  };
}
