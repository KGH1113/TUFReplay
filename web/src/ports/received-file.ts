/** A file opened in the game for repeated reads by URL. Nothing is copied into the page. */
export interface OpenedTransferredFile {
  readonly url: string;
  /** Releases the file in the game. Never rejects. */
  close(): Promise<void>;
}

/** Application-facing capabilities of a single-use file received from the game. */
export interface TransferredFile {
  readonly name: string;
  readonly type: string;
  readonly size: number;
  stream(options?: { signal?: AbortSignal }): ReadableStream<Uint8Array>;
  /**
   * Opens the file so a media element can stream and seek it by URL. Missing or
   * rejecting when the game's IPC runtime cannot; the file stays unread then.
   */
  open?(options?: { signal?: AbortSignal }): Promise<OpenedTransferredFile>;
}

export interface RecordingDownload {
  /** Hands the file to the browser's native download manager without buffering it. */
  download(): Promise<void>;
}
