import type { ActivityChart, RunMarker } from "../activity.model";

const PROTOCOL = "web-adofai.chart";
const VERSION = 1;

interface Envelope {
  protocol: string;
  version: number;
  type: string;
}

export interface ChartBridgeCallbacks {
  onReady(): void;
  onLoaded(): void;
  onError(message: string): void;
  onFloorSelected(floorIndex: number): void;
  onMarkerSelected(markerId: string, floorIndex: number): void;
}

export class ChartBridge {
  private revision = 0;
  private requestId = "";
  private ready = false;
  private chart: ActivityChart | null = null;
  private markers: RunMarker[] = [];
  private markerSignature = "";

  constructor(
    private readonly frame: HTMLIFrameElement,
    private readonly origin: string,
    private readonly callbacks: ChartBridgeCallbacks,
  ) {}

  handleMessage(event: MessageEvent) {
    if (
      event.origin !== this.origin ||
      event.source !== this.frame.contentWindow ||
      !isEnvelope(event.data)
    )
      return;
    const message = event.data;
    if (isReadyMessage(message)) {
      this.ready = true;
      this.callbacks.onReady();
      this.sendChart();
      return;
    }
    if (isLoadedMessage(message)) {
      if (message.requestId === this.requestId) this.callbacks.onLoaded();
    } else if (isErrorMessage(message)) {
      if (message.requestId === this.requestId) this.callbacks.onError(message.message);
    } else if (isFloorSelectedMessage(message)) {
      this.callbacks.onFloorSelected(message.floorIndex);
    } else if (isMarkerSelectedMessage(message)) {
      this.callbacks.onMarkerSelected(message.id, message.floorIndex);
    }
  }

  load(chart: ActivityChart) {
    this.chart = chart;
    this.sendChart();
  }

  setMarkers(markers: RunMarker[]) {
    const signature = markerSetSignature(markers);
    if (signature === this.markerSignature) return;
    this.markers = markers;
    this.markerSignature = signature;
    this.sendMarkers();
  }

  focus(floorIndex: number, markerId?: string) {
    if (!this.ready) return;
    this.frame.contentWindow?.postMessage(
      envelope("chart.focus", { floorIndex, markerId }),
      this.origin,
    );
  }

  focusRun(startFloorIndex: number, endFloorIndex: number) {
    if (!this.ready) return;
    this.frame.contentWindow?.postMessage(
      envelope("run.focus", { startFloorIndex, endFloorIndex }),
      this.origin,
    );
  }

  fitEntireRun(startFloorIndex: number, endFloorIndex: number) {
    if (!this.ready) return;
    this.frame.contentWindow?.postMessage(
      envelope("run.fit-all", { startFloorIndex, endFloorIndex }),
      this.origin,
    );
  }

  clearRunFocus() {
    if (!this.ready) return;
    this.frame.contentWindow?.postMessage(envelope("run.clear", {}), this.origin);
  }

  clear() {
    this.chart = null;
    this.markers = [];
    this.markerSignature = "";
    if (!this.ready) return;
    this.frame.contentWindow?.postMessage(envelope("chart.clear", {}), this.origin);
  }

  private sendChart() {
    if (!this.ready || !this.chart) return;
    this.requestId = `chart-${this.revision + 1}-${crypto.randomUUID()}`;
    this.frame.contentWindow?.postMessage(
      envelope("chart.load", { requestId: this.requestId, levelText: this.chart.LevelText }),
      this.origin,
    );
    this.sendMarkers();
  }

  private sendMarkers() {
    if (!this.ready) return;
    this.revision += 1;
    this.frame.contentWindow?.postMessage(
      envelope("markers.set", { revision: this.revision, markers: this.markers }),
      this.origin,
    );
  }
}

function markerSetSignature(markers: readonly RunMarker[]) {
  return markers
    .map(
      (marker) =>
        `${marker.id}:${marker.floorIndex}:${marker.count}:${marker.clearCount}:${marker.bestLastFloorIndex}`,
    )
    .join("|");
}

function isEnvelope(input: unknown): input is Envelope & Record<string, unknown> {
  if (!input || typeof input !== "object") return false;
  const value = input as Record<string, unknown>;
  return value.protocol === PROTOCOL && value.version === VERSION && typeof value.type === "string";
}

function envelope(type: string, payload: object) {
  return { protocol: PROTOCOL, version: VERSION, type, ...payload };
}

function isReadyMessage(message: Envelope & Record<string, unknown>) {
  return message.type === "chart.ready";
}

function isLoadedMessage(
  message: Envelope & Record<string, unknown>,
): message is Envelope & Record<string, unknown> & { requestId: string; floorCount: number } {
  return (
    message.type === "chart.loaded" &&
    typeof message.requestId === "string" &&
    isFloorIndex(message.floorCount)
  );
}

function isErrorMessage(
  message: Envelope & Record<string, unknown>,
): message is Envelope &
  Record<string, unknown> & { requestId: string; code: string; message: string } {
  return (
    message.type === "chart.error" &&
    typeof message.requestId === "string" &&
    typeof message.code === "string" &&
    typeof message.message === "string"
  );
}

function isFloorSelectedMessage(
  message: Envelope & Record<string, unknown>,
): message is Envelope & Record<string, unknown> & { floorIndex: number } {
  return message.type === "chart.floor-selected" && isFloorIndex(message.floorIndex);
}

function isMarkerSelectedMessage(
  message: Envelope & Record<string, unknown>,
): message is Envelope & Record<string, unknown> & { id: string; floorIndex: number } {
  return (
    message.type === "chart.marker-selected" &&
    typeof message.id === "string" &&
    message.id.length > 0 &&
    isFloorIndex(message.floorIndex)
  );
}

function isFloorIndex(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0;
}
