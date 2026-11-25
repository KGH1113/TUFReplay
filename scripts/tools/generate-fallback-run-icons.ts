#!/usr/bin/env bun

// Draws the judgment-difficulty and No-Fail icons the web companion shows when the game
// cannot hand over ADOFAI's own. TUFReplay draws these itself, so the repository ships no
// game art. Run it again after changing a shape: the output is deterministic.

import { join } from "node:path";
import { deflateSync } from "node:zlib";

type Rgb = readonly [number, number, number];
type Distance = (x: number, y: number) => number;
type Layer = { color: Rgb; shape: Distance; opacity?: number; erase?: boolean };

const size = 96;
const center = size / 2;
const projectRoot = join(import.meta.dir, "../..");
const outputDirectory = join(projectRoot, "web/src/shared/assets/activity");

const ringColor: Rgb = [161, 161, 170];
const white: Rgb = [244, 244, 245];

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

// Signed distances in pixels: negative inside, positive outside.
const disk =
  (radius: number): Distance =>
  (x, y) =>
    Math.hypot(x - center, y - center) - radius;
const ring =
  (radius: number, width: number): Distance =>
  (x, y) =>
    Math.abs(Math.hypot(x - center, y - center) - radius) - width / 2;
const union =
  (...shapes: Distance[]): Distance =>
  (x, y) =>
    Math.min(...shapes.map((shape) => shape(x, y)));
const stroke =
  (ax: number, ay: number, bx: number, by: number, width: number): Distance =>
  (x, y) => {
    const px = x - ax;
    const py = y - ay;
    const dx = bx - ax;
    const dy = by - ay;
    const t = clamp((px * dx + py * dy) / (dx * dx + dy * dy), 0, 1);
    return Math.hypot(px - dx * t, py - dy * t) - width / 2;
  };
const polygon =
  (points: readonly (readonly [number, number])[]): Distance =>
  (x, y) => {
    let distance = Number.POSITIVE_INFINITY;
    let sign = 1;
    for (let i = 0, j = points.length - 1; i < points.length; j = i, i++) {
      const [ix, iy] = points[i];
      const [jx, jy] = points[j];
      const ex = jx - ix;
      const ey = jy - iy;
      const wx = x - ix;
      const wy = y - iy;
      const t = clamp((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1);
      distance = Math.min(distance, Math.hypot(wx - ex * t, wy - ey * t));
      const above = y >= iy;
      const below = y < jy;
      const left = ex * wy > ey * wx;
      if ((above && below && left) || (!above && !below && !left)) sign = -sign;
    }
    return sign * distance;
  };

function quadratic(
  from: readonly [number, number],
  control: readonly [number, number],
  to: readonly [number, number],
  steps = 24,
): [number, number][] {
  return Array.from({ length: steps }, (_, step) => {
    const t = step / steps;
    const u = 1 - t;
    return [
      u * u * from[0] + 2 * u * t * control[0] + t * t * to[0],
      u * u * from[1] + 2 * u * t * control[1] + t * t * to[1],
    ];
  });
}

/** A target whose coloured area shows how wide the judgment window is. */
function bullseye(color: Rgb, fillRadius: number): Layer[] {
  return [
    { color, shape: disk(fillRadius), opacity: 0.92 },
    { color: ringColor, shape: union(ring(41, 6), ring(25, 6)) },
  ];
}

function shieldWithCheck(): Layer[] {
  const outline = [
    ...quadratic([18, 22], [48, 10], [78, 22]),
    ...quadratic([78, 22], [78, 35], [78, 48], 4),
    ...quadratic([78, 48], [78, 76], [48, 90]),
    ...quadratic([48, 90], [18, 76], [18, 48]),
    ...quadratic([18, 48], [18, 35], [18, 22], 4),
  ];
  return [
    { color: white, shape: polygon(outline) },
    { color: white, shape: union(stroke(34, 53, 44, 63, 9), stroke(44, 63, 63, 41, 9)), erase: true },
  ];
}

function render(layers: Layer[]): Uint8Array {
  const pixels = new Uint8Array(size * size * 4);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let red = 0;
      let green = 0;
      let blue = 0;
      let alpha = 0;
      for (const layer of layers) {
        const coverage = clamp(0.5 - layer.shape(x + 0.5, y + 0.5), 0, 1) * (layer.opacity ?? 1);
        if (coverage === 0) continue;
        if (layer.erase) {
          alpha *= 1 - coverage;
          continue;
        }
        const nextAlpha = coverage + alpha * (1 - coverage);
        const keep = (alpha * (1 - coverage)) / nextAlpha;
        const add = coverage / nextAlpha;
        red = layer.color[0] * add + red * keep;
        green = layer.color[1] * add + green * keep;
        blue = layer.color[2] * add + blue * keep;
        alpha = nextAlpha;
      }
      const offset = (y * size + x) * 4;
      const opaque = Math.round(alpha * 255);
      pixels[offset] = opaque === 0 ? 0 : Math.round(red);
      pixels[offset + 1] = opaque === 0 ? 0 : Math.round(green);
      pixels[offset + 2] = opaque === 0 ? 0 : Math.round(blue);
      pixels[offset + 3] = opaque;
    }
  }
  return pixels;
}

const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});

function crc32(bytes: Uint8Array): number {
  let crc = 0xffffffff;
  for (const byte of bytes) crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type: string, data: Uint8Array): Uint8Array {
  const out = new Uint8Array(12 + data.length);
  const view = new DataView(out.buffer);
  view.setUint32(0, data.length);
  out.set(new TextEncoder().encode(type), 4);
  out.set(data, 8);
  view.setUint32(8 + data.length, crc32(out.subarray(4, 8 + data.length)));
  return out;
}

function encodePng(pixels: Uint8Array): Uint8Array {
  const header = new Uint8Array(13);
  const view = new DataView(header.buffer);
  view.setUint32(0, size);
  view.setUint32(4, size);
  header.set([8, 6, 0, 0, 0], 8);
  const rows = new Uint8Array(size * (size * 4 + 1));
  for (let y = 0; y < size; y++) {
    rows.set(pixels.subarray(y * size * 4, (y + 1) * size * 4), y * (size * 4 + 1) + 1);
  }
  const parts = [
    new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", header),
    chunk("IDAT", new Uint8Array(deflateSync(rows, { level: 9 }))),
    chunk("IEND", new Uint8Array()),
  ];
  const png = new Uint8Array(parts.reduce((total, part) => total + part.length, 0));
  let offset = 0;
  for (const part of parts) {
    png.set(part, offset);
    offset += part.length;
  }
  return png;
}

const icons: Record<string, Layer[]> = {
  "lenient-bullseye.png": bullseye([74, 222, 128], 44),
  "normal-bullseye.png": bullseye(white, 29),
  "strict-bullseye.png": bullseye([248, 113, 113], 14),
  "no-fail.png": shieldWithCheck(),
};

for (const [name, layers] of Object.entries(icons)) {
  const path = join(outputDirectory, name);
  await Bun.write(path, encodePng(render(layers)));
  console.log(`Wrote ${path}`);
}
