import type { TransferredFile } from "@/ports/received-file";

/** Small images from the game, read whole into the page. */
export interface ImageDownloads {
  readPng(file: TransferredFile, options?: { signal?: AbortSignal }): Promise<Blob>;
}
