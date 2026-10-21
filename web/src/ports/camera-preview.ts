export interface CameraPreviewFrame {
  bytes: Uint8Array<ArrayBuffer>;
  width: number;
  height: number;
}

export interface CameraPreviewDownloads {
  readFrame(
    frame: { file: TransferredFile; width: number; height: number },
    options?: { signal?: AbortSignal },
  ): Promise<CameraPreviewFrame>;
}

import type { TransferredFile } from "@/ports/received-file";
