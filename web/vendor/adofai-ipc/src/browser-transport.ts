import { IpcUnavailableError } from "./errors";
import type { IpcTransport, IpcTransportFactory } from "./types";

export const browserTransportFactory: IpcTransportFactory = (url, protocol) => new BrowserTransport(url, protocol);

class BrowserTransport implements IpcTransport {
  private socket: WebSocket | undefined;
  private closed = false;
  private rejectConnect: ((reason: unknown) => void) | undefined;
  private readonly messages = new Set<(text: string) => void>();
  private readonly closes = new Set<(reason?: unknown) => void>();
  constructor(private readonly url: string, private readonly protocol: string) {}
  connect(): Promise<void> {
    if (this.closed || typeof globalThis.WebSocket !== "function") return Promise.reject(new IpcUnavailableError("A WebSocket transport is required."));
    return new Promise((resolve, reject) => {
      this.rejectConnect = reject;
      const socket = new WebSocket(this.url, this.protocol); this.socket = socket;
      globalThis.addEventListener?.("pagehide", this.pageHide);
      socket.onopen = () => { if (this.closed) { socket.close(); reject(new IpcUnavailableError()); } else { this.rejectConnect = undefined; resolve(); } };
      socket.onmessage = (event) => { if (typeof event.data === "string") for (const listener of [...this.messages]) listener(event.data); };
      socket.onerror = (event) => this.failed(event);
      socket.onclose = (event) => this.failed(event);
    });
  }
  send(text: string): void {
    if (this.socket?.readyState !== WebSocket.OPEN || this.closed) throw new IpcUnavailableError();
    if (this.socket.bufferedAmount > 8 * 1024 * 1024) { this.close(); throw new IpcUnavailableError("The IPC send buffer is full. Reconnect to synchronize."); }
    this.socket.send(text);
  }
  close(): void {
    if (this.closed) return;
    this.closed = true;
    globalThis.removeEventListener?.("pagehide", this.pageHide);
    this.rejectConnect?.(new IpcUnavailableError()); this.rejectConnect = undefined;
    const socket = this.socket; this.socket = undefined;
    if (socket) { socket.onopen = null; socket.onmessage = null; socket.onerror = null; socket.onclose = null; try { socket.close(); } catch { /* The browser may already have released a navigating document's socket. */ } }
  }
  private readonly pageHide = () => this.failed(new IpcUnavailableError("The page was hidden for navigation."));
  private failed(reason: unknown): void {
    if (this.closed) return;
    this.close();
    for (const listener of [...this.closes]) { try { listener(reason); } catch { /* Keep transport teardown independent of consumers. */ } }
  }
  onMessage(listener: (text: string) => void): () => void { this.messages.add(listener); return () => this.messages.delete(listener); }
  onClose(listener: (reason?: unknown) => void): () => void { this.closes.add(listener); return () => this.closes.delete(listener); }
}
