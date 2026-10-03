import type {
  RenderExportStatus,
  RenderHealth,
  RenderJob,
  RenderOptions,
} from "@/models/render/render-model";

export interface RenderApi {
  getHealth(): Promise<RenderHealth>;
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
