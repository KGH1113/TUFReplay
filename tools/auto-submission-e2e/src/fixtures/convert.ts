export function trimCsv(
  value: Uint8Array | string,
  columns: number,
  timeColumn: number,
  endTimeUs: number,
) {
  const text = typeof value === "string" ? value : new TextDecoder().decode(value);
  const lines = text.trimEnd().split("\n").filter(Boolean);
  if (!lines.length) throw new Error("empty recording stream");
  return lines
    .filter((line) => {
      const cells = line.replace(/\r$/, "").split(",");
      if (cells.length !== columns || !/^-?\d+$/.test(cells[timeColumn]))
        throw new Error("missing or invalid record timestamp/columns");
      if (!Number.isSafeInteger(Number(cells[timeColumn]))) throw new Error("unsafe timestamp");
      return Number(cells[timeColumn]) <= endTimeUs;
    })
    .map((line) => line.replace(/\r$/, ""));
}

export function recordingWindow(meta: Record<string, unknown>) {
  const wonTimeUs = meta.wonTimeUs;
  const terminalTimeUs = meta.terminalTimeUs ?? meta.wonTimeUs;
  if (typeof wonTimeUs !== "number" || !Number.isSafeInteger(wonTimeUs) || wonTimeUs <= 0)
    throw new Error("missing clear time");
  if (typeof terminalTimeUs !== "number" || !Number.isSafeInteger(terminalTimeUs) || terminalTimeUs < wonTimeUs)
    throw new Error("invalid terminal time");
  return { wonTimeUs, terminalTimeUs };
}

export function keyCount(lines: string[]) {
  return new Set(
    lines
      .filter((line) => (Number(line.split(",")[2]) & 1) !== 0)
      .map((line) => line.split(",")[1]),
  ).size;
}
