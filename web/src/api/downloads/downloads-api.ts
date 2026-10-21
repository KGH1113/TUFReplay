import { z } from "zod";

export const installStateSchema = z.object({
  Status: z.enum([
    "checking",
    "missing",
    "awaiting-consent",
    "resolving",
    "downloading",
    "extracting",
    "validating",
    "ready",
    "restart-required",
    "declined",
    "cancelled",
    "failed",
  ]),
  InstallationDirectory: z.string().nullish(),
  Source: z.string().nullish(),
  Version: z.string().nullish(),
  Error: z.string().nullish(),
  ErrorCode: z.string().nullish(),
  DownloadedBytes: z.number().nonnegative(),
  TotalBytes: z.number().positive().nullish(),
});
export const downloadsStateSchema = z.object({
  Renderer: installStateSchema,
  Ffmpeg: installStateSchema,
  CameraNeedsFfmpeg: z.boolean(),
});
export type InstallState = z.infer<typeof installStateSchema>;
export type DownloadsState = z.infer<typeof downloadsStateSchema>;
export type DownloadItemId = "renderer" | "ffmpeg";
export type DownloadAction = "request" | "confirm" | "cancel";
export interface DownloadsApi {
  getStatus(): Promise<DownloadsState>;
  act(item: DownloadItemId, action: DownloadAction): Promise<DownloadsState>;
  cancelPendingFfmpeg(): Promise<void>;
}
export function installationBusy(state?: InstallState) {
  return Boolean(
    state &&
      ["checking", "resolving", "downloading", "extracting", "validating"].includes(state.Status),
  );
}
