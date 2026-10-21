import type { TransferredFile } from "@/ports/received-file";

/** File bytes travel separately from bounded local control messages. */
export interface TextDownloads {
  readText(file: TransferredFile, options?: { signal?: AbortSignal }): Promise<string>;
}
