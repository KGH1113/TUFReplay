import { join } from "node:path";
import { mkdir } from "node:fs/promises";
import { dataDir } from "../config";

const input = process.argv[2];
if (!input) throw new Error("Usage: bun src/fixtures/prepare-visuals.ts /path/to/preset.json");
const source = await Bun.file(input).json();
const tab = source.selectedKeyType;
if (!tab || !source.keys?.[tab]) throw new Error("Selected DMNote tab is missing");
for (const key of ["keys", "keyPositions", "statPositions", "graphPositions", "knobPositions", "tabCssOverrides", "tabNoteSettings", "tabNoteOverrides"]) {
  if (source[key] && typeof source[key] === "object") source[key] = tab in source[key] ? { [tab]: source[key][tab] } : {};
}
if (Array.isArray(source.customTabs)) source.customTabs = source.customTabs.filter((entry: { id: string }) => entry.id === tab);
if (Array.isArray(source.tabs)) source.tabs = source.tabs.filter((entry: { id: string }) => entry.id === tab);
source.tabCount = 1;
const output = join(dataDir, "visuals");
await mkdir(output, { recursive: true });
await Bun.write(join(output, "dmnote-numpad.json"), JSON.stringify(source));
for (const font of source.embeddedLocalFonts ?? []) {
  const bytes = font.dataBase64 ?? font.data_base64;
  if (typeof bytes === "string") await Bun.write(join(output, `font-${String(font.fontId).replace(/[^a-zA-Z0-9_-]/g, "_")}.${font.extension ?? "otf"}`), Buffer.from(bytes, "base64"));
}
console.log(JSON.stringify({ selectedTab: tab, output, fonts: source.embeddedLocalFonts?.length ?? 0 }));
