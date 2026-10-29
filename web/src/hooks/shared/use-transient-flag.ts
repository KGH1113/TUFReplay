import { useEffect, useState } from "react";

/** True for `durationMs` after `key` changes to a non-empty value. */
export function useTransientFlag(key: string, durationMs: number) {
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    if (!key) {
      setVisible(false);
      return;
    }
    setVisible(true);
    const timeout = setTimeout(() => setVisible(false), durationMs);
    return () => clearTimeout(timeout);
  }, [durationMs, key]);
  return visible;
}
