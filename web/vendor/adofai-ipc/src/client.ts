import { browserTransportFactory } from "./browser-transport";
import { IpcNamespaceError, IpcProtocolMismatchError, IpcTimeoutError, IpcUnavailableError } from "./errors";
import { IPC_SUBPROTOCOL, MAX_MESSAGE_BYTES, PROTOCOL_VERSION } from "./version";
import type { ConnectIpcOptions, IpcCommandOptions, IpcConnectionState, IpcErrorInfo, IpcMessage, IpcNamespaceStatus, IpcReadyOptions, IpcTransport, IpcTransportFactory } from "./types";

type Listener = (payload: any, message: IpcMessage<any>) => void;
const encoder = new TextEncoder();
let nextId = 0;
function messageId(): string { return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${(++nextId).toString(36)}-${Math.random().toString(36).slice(2)}`; }
function invoke<T>(listeners: Set<(value: T) => void>, value: T): void { for (const listener of [...listeners]) { try { listener(value); } catch { /* A consumer callback cannot break transport dispatch. */ } } }
function parse(text: string): IpcMessage | null {
  if (encoder.encode(text).length > MAX_MESSAGE_BYTES) return null;
  try { const value = JSON.parse(text) as IpcMessage; return value && typeof value.kind === "string" && typeof value.name === "string" && typeof value.id === "string" ? value : null; } catch { return null; }
}

export async function connectIpc(options: ConnectIpcOptions = {}): Promise<IpcConnection> {
  const connection = new IpcConnection(options);
  await connection.start(); return connection;
}

export class IpcConnection {
  private readonly channels = new Map<string, IpcChannel>();
  private readonly states = new Set<(state: IpcConnectionState) => void>();
  private readonly factory: IpcTransportFactory;
  private transport: IpcTransport | undefined;
  private bindings: Array<() => void> = [];
  private stopped = false;
  private recovering = false;
  private _state: IpcConnectionState = "disconnected";
  private readonly timeoutMs: number;
  private readonly startPort: number;
  private readonly endPort: number;
  private readonly host: string;
  private preferredPort: number | undefined;
  private starting: Promise<void> | undefined;
  private cancelProbe: (() => void) | undefined;
  private retryTimer: ReturnType<typeof setTimeout> | undefined;
  private wakeRetry: (() => void) | undefined;
  private heartbeatTimer: ReturnType<typeof setInterval> | undefined;
  private heartbeatDeadline: ReturnType<typeof setTimeout> | undefined;
  private heartbeatId: string | undefined;
  private readonly heartbeatIntervalMs: number;
  private readonly heartbeatTimeoutMs: number;
  peerId: string | undefined;
  connectionId: string | undefined;

  constructor(private readonly options: ConnectIpcOptions = {}) {
    this.factory = options.transportFactory ?? browserTransportFactory;
    // Chromium can delay WebSocket opening by up to five seconds after failed
    // handshakes in the same renderer process. Aborting sooner perpetuates that penalty.
    this.timeoutMs = options.connectTimeoutMs ?? 8000;
    this.heartbeatIntervalMs = options.heartbeatIntervalMs ?? 10000;
    this.heartbeatTimeoutMs = options.heartbeatTimeoutMs ?? 15000;
    this.startPort = options.startPort ?? 32145; this.endPort = options.endPort ?? 32155;
    this.host = options.host ?? "127.0.0.1";
    if (!["127.0.0.1", "localhost"].includes(this.host) || !Number.isInteger(this.startPort) || !Number.isInteger(this.endPort) || this.startPort < 1 || this.endPort > 65535 || this.endPort < this.startPort || this.endPort - this.startPort > 32 || ![this.timeoutMs, this.heartbeatIntervalMs, this.heartbeatTimeoutMs].every(value => Number.isFinite(value) && value > 0)) throw new TypeError("Invalid localhost IPC discovery options.");
  }
  get state(): IpcConnectionState { return this._state; }
  onState(listener: (state: IpcConnectionState) => void): () => void { this.states.add(listener); listener(this._state); return () => this.states.delete(listener); }
  namespace(name: string, protocolMajor?: number): IpcChannel {
    if (protocolMajor !== undefined && (!Number.isInteger(protocolMajor) || protocolMajor < 1)) throw new TypeError("Invalid feature protocol major.");
    if (!/^[a-z0-9-]+$/.test(name)) throw new TypeError("Invalid IPC namespace.");
    let channel = this.channels.get(name);
    if (!channel) { channel = new IpcChannel(this, name, protocolMajor); this.channels.set(name, channel); if (this.state === "connected") this.subscribe(name); }
    if (protocolMajor !== undefined && channel.protocolMajor !== protocolMajor) throw new TypeError("This namespace already uses a different feature protocol requirement.");
    return channel;
  }
  start(): Promise<void> {
    if (this.stopped) return Promise.reject(new IpcUnavailableError());
    if (this.starting) return this.starting;
    if (this.transport || this.recovering) return Promise.resolve();
    this.setState("reconnecting");
    this.starting = this.startOnce().finally(() => { this.starting = undefined; });
    return this.starting;
  }
  private async startOnce(): Promise<void> {
    try { const session = await this.discover(); this.bind(session.transport, session.welcome); }
    catch (error) {
      if (error instanceof IpcProtocolMismatchError) { this.incompatible(error); throw error; }
      if (this.stopped) throw error;
      // Absence is transient: keep the same channels and listeners until the game starts.
      if (!this.recovering) void this.recover();
    }
  }
  close(): void {
    if (this.stopped) return;
    this.shutdown("closed");
  }
  /** Probe a retained socket on browser resume, or wake a pending discovery retry. */
  checkConnection(): void {
    if (this.stopped) return;
    if (this.transport) this.checkHeartbeat(this.transport);
    else this.wakeRetry?.();
  }
  private shutdown(state: "closed" | "incompatible"): void {
    this.stopped = true; clearTimeout(this.retryTimer); this.wakeRetry?.(); this.wakeRetry = undefined;
    this.cancelProbe?.();
    this.clearHeartbeat();
    for (const unbind of this.bindings) unbind(); this.bindings = [];
    const transport = this.transport; this.transport = undefined;
    if (transport) this.disposeTransport(transport);
    for (const channel of this.channels.values()) channel.disconnected();
    this.setState(state);
  }
  send(kind: "command" | "event", namespace: string, name: string, payload: unknown, options: IpcCommandOptions = {}, correlationId?: string): string {
    if (options.signal?.aborted) throw options.signal.reason ?? new DOMException("Aborted", "AbortError");
    if (this.state !== "connected" || !this.transport || this.stopped) throw new IpcUnavailableError();
    if (!/^[a-z0-9.-]+$/.test(name)) throw new TypeError("Invalid IPC message name.");
    const id = options.id ?? messageId();
    if (!id || id.length > 128) throw new TypeError("Invalid IPC message id.");
    this.write({ kind, namespace, name, id, correlationId, payload: payload ?? {} }); return id;
  }
  private subscribe(namespace: string): void { this.write({ kind: "subscribe", namespace, name: "namespace.subscribe", id: messageId(), payload: {} }); }
  private write(message: IpcMessage): void {
    const transport = this.transport;
    if (!transport) throw new IpcUnavailableError();
    const text = JSON.stringify(message);
    if (encoder.encode(text).length > MAX_MESSAGE_BYTES) throw new RangeError("IPC control message exceeds 2 MiB.");
    try { const result = transport.send(text); if (result) void result.catch(() => this.lost(transport)); }
    catch (error) { this.lost(transport); throw error; }
  }
  private async discover(): Promise<{ transport: IpcTransport; welcome: IpcMessage }> {
    const ports = Array.from({ length: this.endPort - this.startPort + 1 }, (_, index) => this.startPort + index);
    if (this.preferredPort !== undefined) {
      ports.splice(ports.indexOf(this.preferredPort), 1);
      ports.unshift(this.preferredPort);
    }
    for (const port of ports) {
      if (this.stopped) throw new IpcUnavailableError();
      const transport = this.factory(`ws://${this.host}:${port}/ipc/ws`, IPC_SUBPROTOCOL);
      let resolveWelcome!: (message: IpcMessage) => void; let rejectWelcome!: (reason: unknown) => void;
      const welcome = new Promise<IpcMessage>((resolve, reject) => { resolveWelcome = resolve; rejectWelcome = reject; });
      // Attach a rejection handler immediately: native transports can close before connect resolves.
      void welcome.catch(() => {});
      const offMessage = transport.onMessage(text => { const message = parse(text); if (message?.kind === "welcome") resolveWelcome(message); });
      const offClose = transport.onClose(reason => rejectWelcome(reason ?? new IpcUnavailableError()));
      let timer: ReturnType<typeof setTimeout> | undefined;
      let disposed = false;
      const dispose = () => { if (!disposed) { disposed = true; this.disposeTransport(transport); } };
      const cancel = () => { rejectWelcome(new IpcUnavailableError()); dispose(); };
      this.cancelProbe = cancel;
      try {
        const verified = await Promise.race([
          (async () => { await transport.connect(); return await welcome; })(),
          welcome,
          new Promise<never>((_, reject) => { timer = setTimeout(() => reject(new IpcTimeoutError(this.timeoutMs)), this.timeoutMs); })
        ]);
        const data = verified.payload as { server?: string; protocolVersion?: number; serverVersion?: string };
        if (data?.server !== "AdofaiIpc") throw new IpcUnavailableError();
        if (data.protocolVersion !== PROTOCOL_VERSION) throw new IpcProtocolMismatchError(data.protocolVersion ?? null, data.serverVersion ?? null);
        if (this.stopped) throw new IpcUnavailableError();
        this.preferredPort = port;
        return { transport, welcome: verified };
      } catch (error) {
        dispose();
        if (error instanceof IpcProtocolMismatchError) throw error;
      } finally { clearTimeout(timer); offMessage(); offClose(); if (this.cancelProbe === cancel) this.cancelProbe = undefined; }
    }
    throw new IpcUnavailableError();
  }
  private bind(transport: IpcTransport, welcome: IpcMessage): void {
    if (this.stopped) { this.disposeTransport(transport); return; }
    this.transport = transport;
    const data = welcome.payload as { peerId: string; connectionId: string }; this.peerId = data.peerId; this.connectionId = data.connectionId;
    this.bindings = [transport.onMessage(text => {
      if (this.transport !== transport || this.stopped) return;
      const message = parse(text); if (!message) { this.lost(transport); return; }
      if (message.kind === "pong" && message.correlationId === this.heartbeatId) {
        clearTimeout(this.heartbeatDeadline); this.heartbeatDeadline = undefined; this.heartbeatId = undefined;
      }
      if (message.kind === "ping") { this.write({ kind: "pong", name: "connection.pong", id: messageId(), correlationId: message.id, payload: {} }); return; }
      if (message.namespace) this.channels.get(message.namespace)?.receive(message);
    }), transport.onClose(() => this.lost(transport))];
    this.write({ kind: "hello", name: "connection.hello", id: messageId(), payload: { protocolVersion: PROTOCOL_VERSION } });
    for (const name of this.channels.keys()) this.subscribe(name);
    if (this.transport !== transport) return;
    this.heartbeatTimer = setInterval(() => this.checkHeartbeat(transport), this.heartbeatIntervalMs);
    this.setState("connected");
  }
  private checkHeartbeat(transport: IpcTransport): void {
    if (this.transport !== transport || this.stopped || this.heartbeatId) return;
    const id = messageId(); this.heartbeatId = id;
    this.heartbeatDeadline = setTimeout(() => this.lost(transport), this.heartbeatTimeoutMs);
    try { this.write({ kind: "ping", name: "connection.ping", id, payload: {} }); }
    catch { /* write already invalidated the failed transport. */ }
  }
  private clearHeartbeat(): void {
    clearInterval(this.heartbeatTimer); clearTimeout(this.heartbeatDeadline);
    this.heartbeatTimer = undefined; this.heartbeatDeadline = undefined; this.heartbeatId = undefined;
  }
  private lost(transport: IpcTransport): void {
    if (this.transport !== transport || this.stopped) return;
    this.clearHeartbeat();
    this.transport = undefined; for (const unbind of this.bindings) unbind(); this.bindings = []; this.disposeTransport(transport);
    this.peerId = undefined; this.connectionId = undefined;
    for (const channel of this.channels.values()) channel.disconnected();
    this.setState("reconnecting");
    if (!this.recovering) void this.recover();
  }
  private async recover(): Promise<void> {
    if (this.recovering || this.stopped) return;
    this.recovering = true; let attempt = 0;
    try {
      while (!this.stopped && !this.transport) {
        await new Promise<void>(resolve => { this.wakeRetry = resolve; this.retryTimer = setTimeout(resolve, Math.min(5000, 250 * 2 ** Math.min(attempt++, 5)) + Math.random() * 150); });
        clearTimeout(this.retryTimer); this.retryTimer = undefined;
        this.wakeRetry = undefined;
        if (this.stopped) return;
        try { const session = await this.discover(); this.bind(session.transport, session.welcome); }
        catch (error) { if (error instanceof IpcProtocolMismatchError) { this.incompatible(error); return; } }
      }
    } finally { this.recovering = false; if (!this.stopped && !this.transport && this.state === "reconnecting") void this.recover(); }
  }
  private incompatible(error: IpcProtocolMismatchError): void { this.shutdown("incompatible"); this.options.onProtocolMismatch?.(error); }
  private setState(state: IpcConnectionState): void { this._state = state; invoke(this.states, state); }
  private disposeTransport(transport: IpcTransport): void { try { const result = transport.close(); if (result) void result.catch(() => {}); } catch { } }
}

export class IpcChannel {
  private readonly events = new Map<string, Set<Listener>>();
  private readonly commands = new Map<string, Set<Listener>>();
  private readonly statuses = new Set<(status: IpcNamespaceStatus, error?: IpcErrorInfo) => void>();
  private _status: IpcNamespaceStatus = "unavailable";
  private _error: IpcErrorInfo | undefined;
  constructor(private readonly connection: IpcConnection, readonly name: string, readonly protocolMajor?: number) {}
  get status(): IpcNamespaceStatus { return this._status; }
  get error(): IpcErrorInfo | undefined { return this._error; }
  on<T = unknown>(name: string, listener: (payload: T, message: IpcMessage<T>) => void): () => void { return this.listen(this.events, name, listener); }
  onCommand<T = unknown>(name: string, listener: (payload: T, message: IpcMessage<T>) => void): () => void { return this.listen(this.commands, name, listener); }
  onStatus(listener: (status: IpcNamespaceStatus, error?: IpcErrorInfo) => void): () => void { this.statuses.add(listener); listener(this.status, this.error); return () => this.statuses.delete(listener); }
  send(name: string, payload?: unknown, options?: IpcCommandOptions): string { this.requireCompatible(); return this.connection.send("command", this.name, name, payload, options); }
  emit(name: string, payload?: unknown): string { this.requireCompatible(); return this.connection.send("event", this.name, name, payload); }
  reply(correlationId: string, name: string, payload?: unknown): string { this.requireCompatible(); return this.connection.send("event", this.name, name, payload, {}, correlationId); }
  whenReady(options: IpcReadyOptions = {}): Promise<void> {
    if (options.signal?.aborted) return Promise.reject(options.signal.reason ?? new DOMException("Aborted", "AbortError"));
    if (this.status === "ready") return Promise.resolve();
    if (this.status === "error") return Promise.reject(new IpcNamespaceError(this.error?.code ?? "namespace_error", this.error?.message ?? "The mod could not initialize."));
    return new Promise((resolve, reject) => {
      let timer: ReturnType<typeof setTimeout> | undefined; let off = () => {};
      const finish = (error?: unknown) => { clearTimeout(timer); off(); options.signal?.removeEventListener("abort", abort); if (error) reject(error); else resolve(); };
      const abort = () => finish(options.signal?.reason ?? new DOMException("Aborted", "AbortError"));
      options.signal?.addEventListener("abort", abort, { once: true });
      off = this.onStatus((status, error) => { if (status === "ready") finish(); else if (status === "error") finish(new IpcNamespaceError(error?.code ?? "namespace_error", error?.message ?? "The mod could not initialize.")); });
      timer = setTimeout(() => finish(new IpcTimeoutError(options.timeoutMs ?? 15000)), options.timeoutMs ?? 15000);
    });
  }
  /** Internal transport adapter dispatch. */
  receive(message: IpcMessage): void {
    if (message.kind === "namespace") {
      let payload = message.payload as { status: IpcNamespaceStatus; error?: IpcErrorInfo; featureProtocolMajor?: number };
      if (payload?.status === "ready" && this.protocolMajor !== undefined && payload.featureProtocolMajor !== this.protocolMajor) {
        payload = { status: "error", error: { code: "feature_protocol_mismatch", message: "The app and mod use different message versions. Update both, restart ADOFAI, and reconnect." } };
      }
      if (["ready", "initializing", "error", "unavailable"].includes(payload?.status)) { this._status = payload.status; this._error = payload.error; for (const listener of [...this.statuses]) { try { listener(this.status, this.error); } catch { } } }
    }
    if (this._error?.code === "feature_protocol_mismatch") return;
    const listeners = (message.kind === "command" ? this.commands : this.events).get(message.name);
    if (listeners) for (const listener of [...listeners]) { try { listener(message.payload, message); } catch { } }
  }
  disconnected(): void { this._status = "unavailable"; this._error = undefined; for (const listener of [...this.statuses]) { try { listener(this.status); } catch { } } }
  private requireCompatible(): void {
    if (this._error?.code === "feature_protocol_mismatch") throw new IpcNamespaceError(this._error.code, this._error.message);
  }
  private listen<T>(map: Map<string, Set<Listener>>, name: string, listener: (payload: T, message: IpcMessage<T>) => void): () => void {
    let listeners = map.get(name); if (!listeners) { listeners = new Set(); map.set(name, listeners); }
    const stored = listener as Listener; listeners.add(stored);
    return () => { listeners?.delete(stored); if (!listeners?.size) map.delete(name); };
  }
}
