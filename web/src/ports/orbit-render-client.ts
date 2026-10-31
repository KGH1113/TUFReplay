export interface OrbitRenderJob {
  id: string;
  state: string;
  progress: number;
  outputPath: string | null;
  error: string | null;
}
export interface OrbitRenderClient {
  ready(options?: { timeoutMs?: number }): Promise<void>;
  health(): Promise<{ ok: boolean; renderer: string }>;
  createRender(request: { options: { replayManifestPath: string } }): Promise<{ id: string }>;
  renderStatus(request: { id: string }): Promise<OrbitRenderJob>;
  cancelRender(request: { id: string }): Promise<unknown>;
}
