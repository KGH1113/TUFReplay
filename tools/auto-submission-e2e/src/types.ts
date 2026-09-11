export type Mode = "accepted" | "unavailable";
export type Scenario = "normal" | "ack-loss" | "receipt-loss" | "gameplay-fail";
export type Speed = 1 | 10 | 0;
export interface Fixture {
  id: string;
  song: string;
  artist?: string;
  creator?: string;
  difficulty?: { id: number; name: string; type: string };
  baseScore?: number;
  downloadUrl?: string;
  actualFileId?: string;
  levelId: number;
  startedAt: string;
  durationUs: number;
  inputCount: number;
  hitCount: number;
  chartSha: string;
  fileId: string;
  supplements: string[];
  meta: Record<string, unknown>;
  judgments: number[];
  keyCount: number;
  speed: number;
}
export interface Frame {
  kind: number;
  timeUs: number;
  payload: Uint8Array;
}
export interface RunOptions {
  fixtureId: string;
  mode: Mode;
  scenario: Scenario;
  speed: Speed;
}
export interface Execution extends RunOptions {
  id: string;
  runId?: string;
  phase: string;
  ack: number;
  totalChunks: number;
  sentChunks: number;
  sentBytes: number;
  playTimeUs?: number;
  playedInputs?: number;
  playedHits?: number;
  error?: string;
  record?: Record<string, unknown>;
  startedAt: string;
}
export interface LogEvent {
  id: number;
  time: string;
  source: "client" | "tuf" | "server" | "tool";
  message: string;
  detail?: unknown;
  runId?: string;
}
