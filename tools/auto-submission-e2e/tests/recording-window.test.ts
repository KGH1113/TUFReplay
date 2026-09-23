import { expect, test } from "bun:test";
import { keyCount, recordingWindow, trimCsv } from "../src/fixtures/convert";

test("keeps post-clear key releases through terminal but excludes tail keys from submission count", () => {
  const { wonTimeUs, terminalTimeUs } = recordingWindow({ wonTimeUs: 100, terminalTimeUs: 300 });
  const csv = "-10,1,3,1,0\n50,1,2,1,0\n200,2,3,2,0\n300,2,2,2,0\n301,3,3,3,0\n";
  expect(trimCsv(csv, 5, 0, terminalTimeUs)).toHaveLength(4);
  expect(keyCount(trimCsv(csv, 5, 0, wonTimeUs))).toBe(1);
});

test("legacy recordings fall back to clear, but invalid terminal times fail", () => {
  expect(recordingWindow({ wonTimeUs: 100 })).toEqual({ wonTimeUs: 100, terminalTimeUs: 100 });
  for (const terminalTimeUs of [99, -1, 100.5, Infinity])
    expect(() => recordingWindow({ wonTimeUs: 100, terminalTimeUs })).toThrow();
});
