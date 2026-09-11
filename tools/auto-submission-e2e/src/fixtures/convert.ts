export function trimCsv(
  value: Uint8Array | string,
  columns: number,
  timeColumn: number,
  wonTimeUs: number,
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
      return Number(cells[timeColumn]) <= wonTimeUs;
    })
    .map((line) => line.replace(/\r$/, ""));
}

export function keyCount(lines: string[]) {
  return new Set(
    lines
      .filter((line) => (Number(line.split(",")[2]) & 1) !== 0)
      .map((line) => line.split(",")[1]),
  ).size;
}
