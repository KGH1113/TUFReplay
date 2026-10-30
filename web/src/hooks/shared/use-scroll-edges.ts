import { type RefObject, useEffect, useState } from "react";

/** Whether a scroll container has hidden content above or below its visible area. */
export function useScrollEdges(ref: RefObject<HTMLElement | null>) {
  const [edges, setEdges] = useState({ above: false, below: false });
  useEffect(() => {
    const element = ref.current;
    if (!element) return;
    let frame = 0;
    const update = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        const above = element.scrollTop > 1;
        const below = element.scrollTop + element.clientHeight < element.scrollHeight - 1;
        setEdges((current) =>
          current.above === above && current.below === below ? current : { above, below },
        );
      });
    };
    update();
    element.addEventListener("scroll", update, { passive: true });
    // Expanding rows animate their height, which changes the content without a scroll or resize.
    element.addEventListener("transitionend", update);
    const observer = new ResizeObserver(update);
    observer.observe(element);
    // Rows expand when a day is selected, which changes height without resizing the scroller.
    const mutations = new MutationObserver(update);
    mutations.observe(element, { childList: true, subtree: true });
    return () => {
      cancelAnimationFrame(frame);
      element.removeEventListener("scroll", update);
      element.removeEventListener("transitionend", update);
      observer.disconnect();
      mutations.disconnect();
    };
  }, [ref]);
  return edges;
}
