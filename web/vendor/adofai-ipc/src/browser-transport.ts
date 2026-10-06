import { IpcUnavailableError } from "./errors";
import type { IpcTransport, IpcTransportFactory } from "./types";

export const browserTransportFactory: IpcTransportFactory = (url, protocol) => new BrowserTransport(url, protocol);

class BrowserTransport implements IpcTransport {
  private socket: WebSocket | undefined;
  private closed = false;
  private readonly messages = new Set<(text: string) => void>();
  private readonly closes = new Set<(reason?: unknown) => void>();
  constructor(private readonly url: string, private readonly protocol: string) {}
  connect(): Promise<void> {
    if (this.closed || typeof globalThis.WebSocket !== "function") return Promise.reject(new IpcUnavailableError("A WebSocket transport is required."));
    return new Promise((resolve, reject) => {
      const socket = new WebSocket(this.url, this.protocol); this.socket = socket;
      socket.onopen = () => { if (this.closed) { socket.close(); reject(new IpcUnavailableError()); } else resolve(); };
      socket.onmessage = (event) => { if (typeof event.data === "string") for (const listener of [...this.messages]) listener(event.data); };
      socket.onerror = () => reject(new IpcUnavailableError());
      socket.onclose = (event) => { reject(new IpcUnavailableError()); for (const listener of [...this.closes]) listener(event); };
    });
  }
  send(text: string): void {
    if (this.socket?.readyState !== WebSocket.OPEN || this.closed) throw new IpcUnavailableError();
    if (this.socket.bufferedAmount > 8 * 1024 * 1024) { this.close(); throw new IpcUnavailableError("The IPC send buffer is full. Reconnect to synchronize."); }
    this.socket.send(text);
  }
  close(): void { this.closed = true; this.socket?.close(); }
  onMessage(listener: (text: string) => void): () => void { this.messages.add(listener); return () => this.messages.delete(listener); }
  onClose(listener: (reason?: unknown) => void): () => void { this.closes.add(listener); return () => this.closes.delete(listener); }
}
