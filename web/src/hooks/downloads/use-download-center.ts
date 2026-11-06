import { useEffect, useRef, useState } from "react";
import type { DownloadItemId } from "@/api/downloads/downloads-api";
import { useDownloads } from "./use-downloads";

/** Why a download dialog opened outside the download center. */
export type DownloadDialogReason = "render";

export interface DownloadDialogTarget {
  item: DownloadItemId;
  reason: DownloadDialogReason | null;
}

export type DownloadCenterControl = ReturnType<typeof useDownloadCenter>;

export function useDownloadCenter(enabled: boolean) {
  const downloads = useDownloads(enabled);
  const [menuOpen, setMenuOpen] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  // Kept after closing so the dialog keeps its content while it fades out.
  const [dialog, setDialog] = useState<DownloadDialogTarget>({ item: "ffmpeg", reason: null });
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
      const wasAwaitingConsent = prompted.current[id];
      if (status === "awaiting-consent" && !prompted.current[id]) {
        setMenuOpen(false);
        if (currentItem.current !== id) {
          returnFocus.current =
            document.activeElement instanceof HTMLElement ? document.activeElement : null;
          currentItem.current = id;
          autoClose.current = id === "ffmpeg";
          setDialog({ item: id, reason: null });
          setDialogOpen(true);
        }
      }
      // A peer completion is verified in the background before it becomes ready.
      prompted.current[id] =
        status === "awaiting-consent" || (status === "checking" && wasAwaitingConsent);
      if (
        currentItem.current === id &&
        status === "ready" &&
        (autoClose.current || (id === "ffmpeg" && wasAwaitingConsent))
      ) {
        currentItem.current = null;
        autoClose.current = false;
        setDialogOpen(false);
      }
    }
  }, [enabled, rendererStatus, ffmpegStatus]);

  return {
    ...downloads,
    act: (id: DownloadItemId, action: Parameters<typeof downloads.act>[1]) => {
      downloads.act(id, action);
    },
    menuOpen,
    setMenuOpen,
    dialog,
    dialogOpen,
    trigger,
    /**
     * Opens one component's dialog. `request` asks the game for install consent right away;
     * `from` names why it opened and where focus goes back afterwards.
     */
    showDetails: (
      next: DownloadItemId,
      request = false,
      from?: { reason?: DownloadDialogReason; focus?: HTMLElement | null },
    ) => {
      returnFocus.current = from?.focus ?? trigger.current;
      setMenuOpen(false);
      currentItem.current = next;
      autoClose.current = false;
      setDialog({ item: next, reason: from?.reason ?? null });
      setDialogOpen(true);
      if (request) downloads.act(next, "request");
    },
    close: () => {
      const state =
        dialog.item === "renderer" ? downloads.state?.Renderer : downloads.state?.Ffmpeg;
      if (dialogOpen && state?.Status === "awaiting-consent") downloads.act(dialog.item, "cancel");
      currentItem.current = null;
      autoClose.current = false;
      setDialogOpen(false);
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
