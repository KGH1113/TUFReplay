import type {
  OutputDirectorySelection,
  RenderExportStatus,
  RenderHealth,
  RenderJob,
  RenderOptions,
  RenderSettings,
} from "@/models/render/render-model";

export interface RenderApi {
  getHealth(): Promise<RenderHealth>;
  getSettings(): Promise<RenderSettings>;
  updateSettings(options: Partial<RenderOptions>): Promise<RenderSettings>;
  chooseOutputDirectory(initialPath?: string): Promise<OutputDirectorySelection>;
  getOutputDirectorySelection(selectionId: string): Promise<OutputDirectorySelection>;
  cancelOutputDirectorySelection(selectionId: string): Promise<{ cancelled: boolean }>;
  openOutputDirectory(jobId?: string): Promise<{ opened: boolean }>;
  exportBundle(
    runId: string,
    options: RenderOptions,
    levelPath?: string,
  ): Promise<RenderExportStatus>;
  getExportStatus(jobId: string): Promise<RenderExportStatus>;
  cancelExport(jobId: string): Promise<RenderExportStatus>;
  start(manifestPath: string, options: RenderOptions): Promise<RenderJob>;
  getStatus(jobId: string): Promise<RenderJob>;
  cancel(jobId: string): Promise<RenderJob>;
  prepareDownload(jobId: string): Promise<string>;
}
