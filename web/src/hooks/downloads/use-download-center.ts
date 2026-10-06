import { useEffect, useRef, useState } from "react";
import type { DownloadItemId } from "@/api/downloads/downloads-api";
import { useDownloads } from "./use-downloads";

export function useDownloadCenter(enabled: boolean) {
  const downloads = useDownloads(enabled);
  const [menuOpen, setMenuOpen] = useState(false);
  const [item, setItem] = useState<DownloadItemId | null>(null);
  const currentItem = useRef<DownloadItemId | null>(null);
  const autoClose = useRef(false);
  const prompted = useRef({ renderer: false, ffmpeg: false });
  const trigger = useRef<HTMLButtonElement>(null);
  const returnFocus = useRef<HTMLElement | null>(null);
  const rendererStatus = downloads.state?.Renderer.Status;
  const ffmpegStatus = downloads.state?.Ffmpeg.Status;
  useEffect(() => {
    if (!enabled) return;
    for (const [id, status] of [
      ["renderer", rendererStatus],
      ["ffmpeg", ffmpegStatus],
    ] as const) {
      if (status === "awaiting-consent" && !prompted.current[id]) {
        setMenuOpen(false);
        if (currentItem.current !== id) {
          returnFocus.current =
            document.activeElement instanceof HTMLElement ? document.activeElement : null;
          currentItem.current = id;
          autoClose.current = id === "ffmpeg";
          setItem(id);
        }
      }
      prompted.current[id] = status === "awaiting-consent";
      if (currentItem.current === id && autoClose.current && status === "ready") {
        currentItem.current = null;
        autoClose.current = false;
        setItem(null);
      }
    }
  }, [enabled, rendererStatus, ffmpegStatus]);

  return {
    ...downloads,
    menuOpen,
    setMenuOpen,
    item,
    trigger,
    showDetails: (next: DownloadItemId, request = false) => {
      returnFocus.current = trigger.current;
      setMenuOpen(false);
      currentItem.current = next;
      autoClose.current = false;
      setItem(next);
      if (request) downloads.act(next, "request");
    },
    close: () => {
      const state = item === "renderer" ? downloads.state?.Renderer : downloads.state?.Ffmpeg;
      if (item && state?.Status === "awaiting-consent") downloads.act(item, "cancel");
      currentItem.current = null;
      autoClose.current = false;
      setItem(null);
    },
    restoreFocus: () => {
      const previous = returnFocus.current;
      if (!previous?.isConnected) return;
      const target = previous.hasAttribute("disabled")
        ? previous
            .closest('[role="dialog"]')
            ?.querySelector<HTMLElement>("button:not(:disabled), a[href], input:not(:disabled)")
        : previous;
      target?.focus();
    },
  };
}
