import type { OpenedTransferredFile, TransferredFile } from "@/ports/received-file";

/**
 * The largest video copied from the game into the page to preview it, when the
 * game cannot stream it. Bigger ones (a 4K ProRes minute is about 10 GB) are
 * watched with Open video.
 */
export const previewSizeLimit = 1024 ** 3;

export type PreviewSkip = "format" | "size";

/**
 * Why a finished video is not previewed in the page, or null when it can be.
 * A streamed video plays at any size; a copied one must fit the size limit.
 */
export function previewSkipReason(
  file: { type: string; size: number },
  canPlay: (type: string) => boolean,
  streamed = false,
): PreviewSkip | null {
  if (!file.type || !canPlay(file.type)) return "format";
  if (!streamed && file.size > previewSizeLimit) return "size";
  return null;
}

/**
 * Opens the video in the game so the player streams only what it shows, or
 * returns null when the game cannot. The file stays unread then, so it can
 * still be copied.
 */
export async function openForPreview(
  file: TransferredFile,
  signal: AbortSignal,
): Promise<OpenedTransferredFile | null> {
  if (!file.open) return null;
  try {
    return await file.open({ signal });
  } catch (error) {
    if (signal.aborted) throw error;
    return null;
  }
}
