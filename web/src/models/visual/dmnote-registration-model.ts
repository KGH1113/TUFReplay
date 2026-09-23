export interface DmnotePlacement {
  x: number;
  y: number;
  /** Older callers may omit this; omitted placements use the original size. */
  scale?: number;
}

export const DMNOTE_VIEWPORT_WIDTH = 1920;
export const DMNOTE_VIEWPORT_HEIGHT = 1080;
export const DMNOTE_SCALE_MIN = 0.1;
export const DMNOTE_SCALE_MAX = 4;
export const DMNOTE_SCALE_DEFAULT = 1;

export interface DmnoteLayout {
  width: number;
  height: number;
  elements: Array<{
    id: string;
    x: number;
    y: number;
    width: number;
    height: number;
    label: string;
  }>;
}
const object = (value: unknown): Record<string, unknown> =>
  value && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
const number = (value: unknown, fallback: number) =>
  typeof value === "number" && Number.isFinite(value) ? value : fallback;

export function normalizeDmnoteScale(value: unknown): number {
  return Math.max(
    DMNOTE_SCALE_MIN,
    Math.min(DMNOTE_SCALE_MAX, number(value, DMNOTE_SCALE_DEFAULT)),
  );
}

export function dmnoteScaledDimensions(
  layout: DmnoteLayout,
  scale?: unknown,
): { width: number; height: number; scale: number } {
  const actualScale = normalizeDmnoteScale(scale);
  const width = Math.max(0, number(layout.width, 0));
  const height = Math.max(0, number(layout.height, 0));
  return {
    width: number(width * actualScale, 0),
    height: number(height * actualScale, 0),
    scale: actualScale,
  };
}

function selectedTabPreset(json: string): Record<string, unknown> {
  const preset = object(JSON.parse(json));
  const keys = object(preset.keys);
  const tabs = Object.keys(keys);
  if (!tabs.length) throw new Error("visual_bundle_invalid");
  const selection = preset.selectedKeyType ?? preset.selected_key_type;
  const tab =
    selection == null || selection === "" ? (tabs.length === 1 ? tabs[0] : undefined) : selection;
  if (typeof tab !== "string" || !Object.hasOwn(keys, tab))
    throw new Error("visual_selected_tab_missing");
  if (!Array.isArray(keys[tab])) throw new Error("visual_bundle_invalid");
  for (const field of [
    "keys",
    "keyPositions",
    "statPositions",
    "graphPositions",
    "knobPositions",
    "tabCssOverrides",
    "tabCSSOverrides",
    "tab_css_overrides",
    "tabNoteSettings",
    "tabNoteOverrides",
    "tab_note_settings",
    "tab_note_overrides",
  ]) {
    if (preset[field] === undefined) continue;
    const map = preset[field];
    if (!map || typeof map !== "object" || Array.isArray(map))
      throw new Error("visual_bundle_invalid");
    preset[field] = Object.hasOwn(map, tab) ? { [tab]: object(map)[tab] } : {};
  }
  for (const field of [
    "tabs",
    "tabList",
    "tab_definitions",
    "tabDefinitions",
    "customTabs",
    "custom_tabs",
    "viewerTabs",
    "keyviewerTabs",
  ]) {
    if (Array.isArray(preset[field]))
      preset[field] = preset[field].filter((item) => object(item).id === tab);
  }
  for (const field of ["tabCount", "tab_count"]) {
    if (preset[field] !== undefined) preset[field] = 1;
  }
  if (preset.selectedViewerTabs !== undefined)
    preset.selectedViewerTabs = Object.fromEntries(
      Object.entries(object(preset.selectedViewerTabs)).filter(([, id]) => id === tab),
    );
  preset.selectedKeyType = tab;
  if (preset.selected_key_type !== undefined) preset.selected_key_type = tab;
  return preset;
}

export function dmnoteRegistrationLayout(json: string): DmnoteLayout {
  const preset = selectedTabPreset(json);
  const keys = object(preset.keys);
  const tabs = Object.keys(keys);
  const tab = tabs[0];
  if (!tab) throw new Error("visual_bundle_invalid");
  const elements: DmnoteLayout["elements"] = [];
  for (const field of ["keyPositions", "statPositions", "graphPositions", "knobPositions"]) {
    const positions = object(preset[field])[tab];
    if (!Array.isArray(positions)) continue;
    positions.forEach((item, index) => {
      const entry = object(item);
      if (entry.hidden === true || entry.visible === false) return;
      const names = keys[tab];
      elements.push({
        id: `${field}:${String(entry.id ?? index)}`,
        x: number(entry.dx, 0),
        y: number(entry.dy, 0),
        width: Math.max(1, number(entry.width, 60)),
        height: Math.max(1, number(entry.height, 60)),
        label:
          typeof entry.displayText === "string" && entry.displayText
            ? entry.displayText
            : field === "keyPositions" && Array.isArray(names)
              ? String(names[index] ?? "")
              : String(entry.statType ?? ""),
      });
    });
  }
  if (!elements.length) throw new Error("visual_bundle_invalid");
  const padding = Math.max(0, number(object(preset.gridSettings).overlayPadding, 30));
  const notes = { ...object(preset.noteSettings), ...object(object(preset.tabNoteOverrides)[tab]) };
  const track = preset.noteEffect === false ? 0 : Math.max(0, number(notes.trackHeight, 300));
  const minX = Math.min(...elements.map((e) => e.x)),
    minY = Math.min(...elements.map((e) => e.y));
  const width = Math.max(...elements.map((e) => e.x + e.width)) - minX + padding * 2;
  const height = Math.max(...elements.map((e) => e.y + e.height)) - minY + padding * 2 + track;
  return {
    width,
    height,
    elements: elements.map((e) => ({
      ...e,
      x: e.x - minX + padding,
      y: e.y - minY + padding + track,
    })),
  };
}

export function clampDmnotePlacement(
  position: DmnotePlacement,
  layout: DmnoteLayout,
): Required<DmnotePlacement> {
  const dimensions = dmnoteScaledDimensions(layout, position.scale);
  const maxX = Math.max(0, Math.floor(DMNOTE_VIEWPORT_WIDTH - dimensions.width));
  const maxY = Math.max(0, Math.floor(DMNOTE_VIEWPORT_HEIGHT - dimensions.height));
  return {
    x: Math.round(Math.max(0, Math.min(maxX, number(position.x, 0)))),
    y: Math.round(Math.max(0, Math.min(maxY, number(position.y, 0)))),
    scale: dimensions.scale,
  };
}

export function prepareDmnoteImport(
  json: string,
  css: string | undefined,
  placement: DmnotePlacement,
  keyCounterEnabled = false,
): string {
  const layout = dmnoteRegistrationLayout(json);
  const preset = selectedTabPreset(json);
  if (css !== undefined) {
    if (new TextEncoder().encode(css).length > 1024 * 1024)
      throw new Error("visual_payload_too_large");
    preset.useCustomCSS = true;
    preset.customCSS = { content: css };
    // An explicitly attached stylesheet is the registration's chosen stylesheet.
    delete preset.tabCssOverrides;
    delete preset.tabCSSOverrides;
    delete preset.tab_css_overrides;
  }
  preset.tufReplayPlacement = clampDmnotePlacement(placement, layout);
  preset.keyCounterEnabled = keyCounterEnabled;
  preset.viewport = { width: DMNOTE_VIEWPORT_WIDTH, height: DMNOTE_VIEWPORT_HEIGHT };
  return JSON.stringify(preset);
}
