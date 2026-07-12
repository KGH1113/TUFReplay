import { useEffect, useRef, useState, type UIEvent } from "react";

import { getChartMetrics, updateScrollState } from "../lib/activity-chart.utils";

interface RunChartViewportOptions {
  domain: { min: number; max: number };
  levelSessionId: string;
  selectedSegmentGroupIndex: number;
  timeDomainMax: number;
}

export function useRunChartViewport(options: RunChartViewportOptions) {
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const [viewportSize, setViewportSize] = useState({ width: 0, height: 0 });
  const [scrollState, setScrollState] = useState({
    canScrollLeft: false,
    canScrollRight: true,
    canScrollUp: false,
    canScrollDown: true,
  });
  const chart = getChartMetrics(options.domain, viewportSize);

  useEffect(() => {
    const element = viewportRef.current;
    if (!element) return;

    const updateViewport = () => {
      setViewportSize({ width: element.clientWidth, height: element.clientHeight });
      updateScrollState(element, setScrollState);
    };

    updateViewport();
    const resizeObserver = new ResizeObserver(updateViewport);
    resizeObserver.observe(element);
    return () => resizeObserver.disconnect();
  }, []);

  useEffect(() => {
    updateScrollState(viewportRef.current, setScrollState);
  }, [
    chart.contentHeight,
    chart.contentWidth,
    options.levelSessionId,
    options.selectedSegmentGroupIndex,
    options.timeDomainMax,
  ]);

  useEffect(() => {
    const element = viewportRef.current;
    if (!element) return;
    element.scrollTo({ left: 0, top: 0 });
    updateScrollState(element, setScrollState);
  }, [options.levelSessionId]);

  const handleScroll = (event: UIEvent<HTMLDivElement>) => {
    updateScrollState(event.currentTarget, setScrollState);
  };

  return { chart, handleScroll, scrollState, viewportRef };
}
