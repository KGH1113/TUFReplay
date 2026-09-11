import { apiBase } from "../config";
import { log } from "../logs";

export class UploadSocket {
  private socket: WebSocket;
  private inbox: any[] = [];
  private waiting?: {
    resolve: (value: any) => void;
    reject: (error: Error) => void;
  };
  private closed = false;
  constructor(
    private readonly runId: string,
    token: string,
  ) {
    // lib.dom omits Bun's documented header-enabled constructor overload.
    const ClientSocket = WebSocket as unknown as new (
      url: string,
      options: Bun.WebSocketOptions,
    ) => WebSocket;
    this.socket = new ClientSocket(
      `${apiBase.replace("http:", "ws:")}/api/v1/runs/${runId}/stream`,
      { headers: { Authorization: `Bearer ${token}` } },
    );
    this.socket.addEventListener("message", (event) => {
      let value;
      try {
        value = JSON.parse(String(event.data));
      } catch {
        this.fail(new Error("invalid WS control"));
        return;
      }
      log("client", `WS ← ${value.type}`, value, runId);
      if (this.waiting) {
        const waiter = this.waiting;
        this.waiting = undefined;
        waiter.resolve(value);
      } else this.inbox.push(value);
    });
    this.socket.addEventListener("close", () => this.fail(new Error("WebSocket closed")));
    this.socket.addEventListener("error", () => this.fail(new Error("WebSocket connection error")));
  }
  async open() {
    if (this.socket.readyState === WebSocket.OPEN) return;
    await new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => {
        this.close();
        reject(new Error("WebSocket open timeout"));
      }, 10000);
      this.socket.addEventListener(
        "open",
        () => {
          clearTimeout(timer);
          resolve();
        },
        { once: true },
      );
      this.socket.addEventListener(
        "error",
        () => {
          clearTimeout(timer);
          reject(new Error("WebSocket open failed"));
        },
        { once: true },
      );
    });
  }
  send(value: unknown) {
    log("client", "WS → control", value, this.runId);
    this.socket.send(JSON.stringify(value));
  }
  binary(bytes: Uint8Array) {
    this.socket.send(new Uint8Array(bytes).buffer);
  }
  close() {
    this.socket.close();
    this.fail(new Error("WebSocket disconnected"));
  }
  private fail(error: Error) {
    this.closed = true;
    this.waiting?.reject(error);
    this.waiting = undefined;
  }
  async next(): Promise<any> {
    if (this.inbox.length) return this.inbox.shift();
    if (this.closed) throw new Error("WebSocket disconnected");
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.waiting = undefined;
        reject(new Error("WebSocket response timeout"));
      }, 15000);
      this.waiting = {
        resolve: (value) => {
          clearTimeout(timer);
          resolve(value);
        },
        reject: (error) => {
          clearTimeout(timer);
          reject(error);
        },
      };
    });
  }
  async until(type: string, minimumAck = -1) {
    for (;;) {
      const control = await this.next();
      if (control.type === "error" || control.type === "nack")
        throw new Error(JSON.stringify(control));
      if (control.type === type && (control.acknowledged_sequence ?? -1) >= minimumAck)
        return control;
    }
  }
}
