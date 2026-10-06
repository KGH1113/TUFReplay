export interface CameraPreviewFrame {
  bytes: Uint8Array<ArrayBuffer>;
  width: number;
  height: number;
}

export interface CameraPreviewDownloads {
  readFrame(
    ticket: { url: string; byteLength: number; metadata: { width: number; height: number } },
    options?: { signal?: AbortSignal },
  ): Promise<CameraPreviewFrame>;
}
