import type { Execution, Fixture, Frame } from "../types";
import { UploadSocket } from "./socket";
import { binaryFrame } from "./frames";
import { log } from "../logs";
import { request } from "./http";

export class Upload {
  private socket?: UploadSocket;
  private heartbeat?: ReturnType<typeof setInterval>;
  private paused = false;
  private lostAck = false;
  private failed = false;
  fail() {
    if (this.execution.phase !== "uploading") throw new Error("No active play");
    this.failed = true;
  }
  private async finishFailed(socket: UploadSocket) {
    clearInterval(this.heartbeat);
    this.execution.phase = "failing";
    socket.send({ type: "fail", reason: "e2e_injected_gameplay_failure" });
    log(
      "client",
      "플레이 실패 주입 — fail 전송, complete·제출 없음",
      undefined,
      this.execution.runId,
    );
    const deadline = Date.now() + 10000;
    while (Date.now() < deadline) {
      const record = await request(`/api/v1/runs/${this.execution.runId}`);
      this.execution.record = record;
      if (["failed", "expired"].includes(record.status)) {
        this.execution.phase = "gameplay_failed";
        return;
      }
      await Bun.sleep(100);
    }
    throw new Error("Server did not confirm failed play");
  }
  constructor(
    private execution: Execution,
    private token: string,
    private interval: number,
  ) {}
  disconnect() {
    if (this.execution.phase !== "uploading") throw new Error("No active upload");
    this.paused = true;
    this.execution.phase = "disconnected";
    this.socket?.close();
    clearInterval(this.heartbeat);
    log("client", "연결 끊음 — 30초 안에 재연결", undefined, this.execution.runId);
  }
  reconnect() {
    if (!this.paused) throw new Error("Upload is not disconnected");
    this.paused = false;
  }
  private async connect() {
    clearInterval(this.heartbeat);
    const socket = new UploadSocket(this.execution.runId!, this.token);
    this.socket = socket;
    await socket.open();
    socket.send({
      type: "hello",
      protocol_version: 1,
      last_acknowledged_sequence: this.execution.ack,
    });
    const ready = await socket.until("ready");
    this.execution.ack = ready.acknowledged_sequence;
    this.execution.phase = "uploading";
    this.heartbeat = setInterval(() => {
      if (!this.paused) socket.send({ type: "heartbeat" });
    }, this.interval);
    return socket;
  }
  async run(frames: Frame[], fixture: Fixture) {
    let inputs = 0,
      hits = 0;
    const decoder = new TextDecoder();
    const progress = frames.map((frame) => {
      if (frame.kind === 0) inputs += decoder.decode(frame.payload).split("\n").length - 1;
      if (frame.kind === 1) hits += decoder.decode(frame.payload).split("\n").length - 1;
      return { playTimeUs: Math.max(0, frame.timeUs), playedInputs: inputs, playedHits: hits };
    });
    let socket = await this.connect();
    let previous = frames[0]?.timeUs ?? 0;
    let lastReplay = -1;
    try {
      for (let index = this.execution.ack + 1; index < frames.length; ) {
        if (this.execution.ack >= 0) Object.assign(this.execution, progress[this.execution.ack]);
        const frame = frames[index];
        if (
          this.failed ||
          (this.execution.scenario === "gameplay-fail" && frame.timeUs >= fixture.durationUs * 0.4)
        ) {
          await this.finishFailed(socket);
          return;
        }
        try {
          if (this.paused) {
            const deadline = Date.now() + 30000;
            while (this.paused && Date.now() < deadline) await Bun.sleep(50);
            if (this.paused) {
              this.paused = false;
              throw new Error("Reconnect window expired");
            }
            socket = await this.connect();
            index = this.execution.ack + 1;
            continue;
          }
          if (this.execution.speed && index !== lastReplay) {
            let delay = Math.max(0, (frame.timeUs - previous) / 1000 / this.execution.speed);
            while (delay > 0 && !this.paused && !this.failed) {
              const step = Math.min(delay, 100);
              await Bun.sleep(step);
              delay -= step;
            }
            if (this.paused) continue;
            if (this.failed) continue;
          }
          previous = frame.timeUs;
          lastReplay = index;
          socket.binary(binaryFrame(frame, index));
          this.execution.sentChunks++;
          this.execution.sentBytes += frame.payload.length;
          if (this.execution.scenario === "ack-loss" && !this.lostAck) {
            this.lostAck = true;
            log("client", "ACK 수신 전 연결 끊김 주입", { sequence: index }, this.execution.runId);
            socket.close();
            clearInterval(this.heartbeat);
            await Bun.sleep(150);
            socket = await this.connect();
            index = this.execution.ack + 1;
            continue;
          }
          const ack = await socket.until("ack", index);
          this.execution.ack = ack.acknowledged_sequence;
          Object.assign(this.execution, progress[this.execution.ack]);
          index = this.execution.ack + 1;
        } catch (error) {
          if (this.paused) continue;
          throw error;
        }
      }
      if (this.failed) {
        await this.finishFailed(socket);
        return;
      }
      socket.send({
        type: "complete",
        final_sequence: frames.length - 1,
        input_count: fixture.inputCount,
        hit_context_count: fixture.hitCount,
      });
      await socket.until("sealed", frames.length - 1);
      this.execution.phase = "saving";
    } finally {
      clearInterval(this.heartbeat);
      this.socket?.close();
    }
  }
}
