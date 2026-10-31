import type {
  OutputDirectorySelection,
  RenderHealth,
  RenderJob,
  RenderOptions,
  RenderPreferences,
  RenderSettings,
} from "@/models/render/render-model";

export interface RenderApi {
  getHealth(): Promise<RenderHealth>;
  getSettings(): Promise<RenderSettings>;
  updateSettings(
    options: Partial<RenderOptions>,
    preferences?: RenderPreferences,
  ): Promise<RenderSettings>;
  chooseOutputDirectory(initialPath?: string): Promise<OutputDirectorySelection>;
  cancelOutputDirectorySelection(selectionId: string): Promise<{ cancelled: boolean }>;
  openOutputDirectory(jobId?: string): Promise<{ opened: boolean }>;
  startRender(runId: string, options: RenderOptions, levelPath?: string): Promise<RenderJob>;
  readRenderState(): Promise<RenderJob | null>;
  cancelRender(jobId: string): Promise<RenderJob>;
  prepareDownload(jobId: string): Promise<string>;
}
