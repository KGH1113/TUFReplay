import { fixturePath } from "../fixtures/store";
import type { Fixture, Frame, Mode } from "../types";

const encoder = new TextEncoder();
export function binaryFrame(frame: Frame, sequence: number): Uint8Array {
  const bytes = new Uint8Array(20 + frame.payload.length);
  bytes.set(encoder.encode("TUFR"));
  const view = new DataView(bytes.buffer);
  view.setUint8(4, 1);
  view.setUint8(5, frame.kind);
  view.setBigUint64(8, BigInt(sequence));
  view.setUint32(16, frame.payload.length);
  bytes.set(frame.payload, 20);
  return bytes;
}

export async function frames(item: Fixture, mode: Mode, maxBytes: number): Promise<Frame[]> {
  const records: { kind: number; timeUs: number; line: string }[] = [];
  for (const [kind, file, timeColumn] of [
    [0, "inputs.csv", 0],
    [1, "hits.csv", 12],
  ] as const) {
    for (const line of (await Bun.file(fixturePath(item, file)).text()).trimEnd().split("\n")) {
      records.push({
        kind,
        timeUs: Number(line.split(",")[timeColumn]),
        line: line + "\n",
      });
    }
  }
  const firstTime = Math.min(0, ...records.map((record) => record.timeUs));
  const state = (kind: number, name: string, timeUs: number) =>
    records.push({
      kind,
      timeUs,
      line:
        JSON.stringify({
          version: 1,
          state: name,
          time_us: timeUs,
          rate: item.speed,
          no_fail: item.meta.noFailMode ?? false,
          difficulty: item.meta.judgmentDifficulty ?? 2,
          dropped: item.meta.inputOverflowDropped ?? 0,
          unmapped: item.meta.inputUnmappedEvents ?? 0,
          hold_behavior: item.meta.submissionHoldBehavior ?? 0,
        }) + "\n",
    });
  state(3, "CaptureStarted", firstTime);
  state(3, "GameplayStarted", 0);
  state(4, "RuntimeSettings", firstTime);
  state(3, "Won", item.durationUs);
  state(5, "RecorderHealth", item.durationUs);
  records.sort((a, b) => a.timeUs - b.timeUs);
  const result: Frame[] = [];
  let pending = "",
    kind = -1,
    bucket = -Infinity,
    timeUs = 0;
  const flush = () => {
    if (pending) result.push({ kind, timeUs, payload: encoder.encode(pending) });
    pending = "";
  };
  const limit = Math.min(16000, maxBytes);
  for (const record of records) {
    const nextBucket = Math.floor(record.timeUs / 250000);
    if (encoder.encode(record.line).length > limit)
      throw new Error("single record exceeds chunk size");
    if (
      record.kind !== kind ||
      nextBucket !== bucket ||
      encoder.encode(pending + record.line).length > limit
    )
      flush();
    kind = record.kind;
    bucket = nextBucket;
    timeUs = record.timeUs;
    pending += record.line;
  }
  flush();
  const meta = encoder.encode(
    JSON.stringify({
      ...item.meta,
      inputCount: item.inputCount,
      hitContextCount: item.hitCount,
      terminalTimeUs: item.durationUs,
      submissionKeyCount: item.keyCount,
      e2e: {
        mode,
        judgments: item.judgments,
        key_count: item.keyCount,
        speed: item.speed,
        is_no_hold_tap: false,
        is_adofai_v2: false,
        supplements: item.supplements,
      },
    }),
  );
  for (let offset = 0; offset < meta.length; offset += limit)
    result.push({
      kind: 2,
      timeUs: item.durationUs,
      payload: meta.slice(offset, offset + limit),
    });
  return result;
}
