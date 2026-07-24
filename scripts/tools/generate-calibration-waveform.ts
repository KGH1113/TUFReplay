#!/usr/bin/env bun

import { join } from "node:path";

const projectRoot = join(import.meta.dir, "../..");
const sourcePath = join(projectRoot, "TUFReplay/Assets/calibration/calibration_old.ogg");
const destinationPath = join(
  projectRoot,
  "TUFReplay/Assets/calibration/calibration_old.waveform",
);
const sampleRate = 44_100;
const framesPerPeak = 32;

const decode = Bun.spawnSync({
  cmd: [
    "ffmpeg",
    "-v",
    "error",
    "-i",
    sourcePath,
    "-f",
    "f32le",
    "-ac",
    "1",
    "-ar",
    String(sampleRate),
    "pipe:1",
  ],
  stdout: "pipe",
  stderr: "inherit",
});
if (decode.exitCode !== 0) throw new Error(`ffmpeg exited with ${decode.exitCode}`);

const pcm = decode.stdout;
const frameCount = Math.floor(pcm.byteLength / 4);
const peakCount = Math.ceil(frameCount / framesPerPeak);
const output = new ArrayBuffer(20 + peakCount * 2);
const bytes = new Uint8Array(output);
bytes.set(new TextEncoder().encode("TUFWRF1\0"), 0);
const view = new DataView(output);
view.setUint32(8, sampleRate, true);
view.setUint32(12, framesPerPeak, true);
view.setUint32(16, peakCount, true);

const samples = new DataView(pcm.buffer, pcm.byteOffset, frameCount * 4);
for (let peakIndex = 0; peakIndex < peakCount; peakIndex++) {
  const firstFrame = peakIndex * framesPerPeak;
  const lastFrame = Math.min(frameCount, firstFrame + framesPerPeak);
  let peak = 0;
  for (let frame = firstFrame; frame < lastFrame; frame++) {
    peak = Math.max(peak, Math.abs(samples.getFloat32(frame * 4, true)));
  }
  view.setUint16(20 + peakIndex * 2, Math.round(Math.min(1, peak) * 65_535), true);
}

await Bun.write(destinationPath, output);
console.log(`Wrote ${peakCount} peaks to ${destinationPath}`);
