import { useLayoutEffect, useRef, useState } from "react";

const REVEAL_SPEED = 50;
const RETURN_SPEED = 400;

export function RelativeLevelPath({ path }: { path: string }) {
  const viewportRef = useRef<HTMLButtonElement>(null);
  const textRef = useRef<HTMLSpanElement>(null);
  const distanceRef = useRef(0);
  const [overflows, setOverflows] = useState(false);

  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    const text = textRef.current;
    if (!viewport || !text) return;

    const measure = () => {
      const distance = Math.max(0, text.scrollWidth - viewport.clientWidth);
      distanceRef.current = distance;
      setOverflows(distance > 1);
      text.style.transition = "none";
      text.style.transform = `translateX(${-distance}px)`;
    };

    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(viewport);
    observer.observe(text);
    return () => observer.disconnect();
  }, []);

  const move = (toStart: boolean) => {
    const text = textRef.current;
    if (
      !text ||
      distanceRef.current <= 1 ||
      window.matchMedia("(prefers-reduced-motion: reduce)").matches
    )
      return;

    const matrix = new DOMMatrixReadOnly(getComputedStyle(text).transform);
    const target = toStart ? 0 : -distanceRef.current;
    const speed = toStart ? REVEAL_SPEED : RETURN_SPEED;
    text.style.transition = "none";
    text.style.transform = `translateX(${matrix.m41}px)`;
    text.getBoundingClientRect();
    text.style.transition = `transform ${Math.abs(target - matrix.m41) / speed}s linear`;
    text.style.transform = `translateX(${target}px)`;
  };

  return (
    <button
      type="button"
      ref={viewportRef}
      title={path}
      onMouseEnter={() => move(true)}
      onMouseLeave={() => move(false)}
      onFocus={() => move(true)}
      onBlur={() => move(false)}
      className={`pointer-events-auto relative z-20 block w-full min-w-0 overflow-hidden whitespace-nowrap border-0 bg-transparent p-0 text-left text-xs text-muted-foreground focus-visible:rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${overflows ? "[mask-image:linear-gradient(to_right,transparent,black_0.75rem,black_calc(100%-0.75rem),transparent)]" : ""}`}
      aria-label={path}
    >
      <span ref={textRef} className="inline-block whitespace-nowrap will-change-transform">
        {path}
      </span>
    </button>
  );
}
