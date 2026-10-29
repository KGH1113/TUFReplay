import { useCallback, useRef, useState } from "react";

/** Live content width of an element, for layouts that place items in pixels. */
export function useElementWidth<T extends HTMLElement>() {
  const [width, setWidth] = useState(0);
  const observerRef = useRef<ResizeObserver | null>(null);
  const ref = useCallback((element: T | null) => {
    observerRef.current?.disconnect();
    observerRef.current = null;
    if (!element) return;
    const update = () => {
      const next = element.clientWidth;
      setWidth((current) => (Math.abs(current - next) < 0.5 ? current : next));
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(element);
    observerRef.current = observer;
  }, []);
  return { ref, width };
}
