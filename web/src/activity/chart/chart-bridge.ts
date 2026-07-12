import type { ActivityChart, RunMarker } from "../activity.model";

const PROTOCOL = "web-adofai.chart";
const VERSION = 1;

interface Envelope { protocol: string; version: number; type: string; requestId?: string; revision?: number }

export interface ChartBridgeCallbacks {
  onReady(): void;
  onLoaded(): void;
  onError(message: string): void;
  onFloorSelected(floorIndex: number): void;
  onMarkerSelected(markerId: string): void;
}

export class ChartBridge {
  private revision = 0;
  private requestId = "";
  private ready = false;
  private pending: { chart: ActivityChart; markers: RunMarker[] } | null = null;

  constructor(
    private readonly frame: HTMLIFrameElement,
    private readonly origin: string,
    private readonly callbacks: ChartBridgeCallbacks,
  ) {}

  handleMessage(event: MessageEvent) {
    if (event.origin !== this.origin || event.source !== this.frame.contentWindow || !isEnvelope(event.data)) return;
    const message = event.data;
    if (message.type === "ready") {
      this.ready = true;
      this.callbacks.onReady();
      this.flush();
      return;
    }
    if (message.requestId !== this.requestId || message.revision !== this.revision) return;
    if (message.type === "chart.loaded") this.callbacks.onLoaded();
    else if (message.type === "chart.error") this.callbacks.onError(typeof message.message === "string" ? message.message : "Chart failed to load");
    else if (message.type === "chart.floor-selected" && typeof message.floorIndex === "number") this.callbacks.onFloorSelected(message.floorIndex);
    else if (message.type === "chart.marker-selected" && typeof message.markerId === "string") this.callbacks.onMarkerSelected(message.markerId);
  }

  load(chart: ActivityChart, markers: RunMarker[]) {
    this.revision += 1;
    this.requestId = `chart-${this.revision}-${crypto.randomUUID()}`;
    this.pending = { chart, markers };
    this.flush();
  }

  focus(floorIndex: number, markerId?: string) {
    if (!this.ready) return;
    this.post("chart.focus", { floorIndex, markerId });
  }

  private flush() {
    if (!this.ready || !this.pending) return;
    this.post("chart.load", { levelText: this.pending.chart.LevelText });
    this.post("markers.set", { markers: this.pending.markers });
    this.pending = null;
  }

  private post(type: string, payload: object) {
    this.frame.contentWindow?.postMessage({ protocol: PROTOCOL, version: VERSION, type, requestId: this.requestId, revision: this.revision, ...payload }, this.origin);
  }
}

function isEnvelope(input: unknown): input is Envelope & Record<string, unknown> {
  if (!input || typeof input !== "object") return false;
  const value = input as Record<string, unknown>;
  return value.protocol === PROTOCOL && value.version === VERSION && typeof value.type === "string";
}
