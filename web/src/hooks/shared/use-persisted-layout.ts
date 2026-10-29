import { useMemo } from "react";
import { useDefaultLayout } from "react-resizable-panels";

/** Remembers a resizable split per browser; storage failures fall back to the default sizes. */
export function usePersistedLayout(id: string) {
  const storage = useMemo(
    () => ({
      getItem(key: string) {
        try {
          return window.localStorage.getItem(key);
        } catch {
          return null;
        }
      },
      setItem(key: string, value: string) {
        try {
          window.localStorage.setItem(key, value);
        } catch {
          // Private windows and blocked storage keep the layout for this visit only.
        }
      },
    }),
    [],
  );
  return useDefaultLayout({ id, storage, onlySaveAfterUserInteractions: true });
}
