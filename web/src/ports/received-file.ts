/** Application-facing capabilities of a single-use file received from the game. */
export interface TransferredFile {
  readonly name: string;
  readonly type: string;
  readonly size: number;
  stream(options?: { signal?: AbortSignal }): ReadableStream<Uint8Array>;
}

export interface RecordingDownload {
  /** Hands the file to the browser's native download manager without buffering it. */
  download(): Promise<void>;
}
