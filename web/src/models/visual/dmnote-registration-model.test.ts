import { expect, test } from "bun:test";
import {
  clampDmnotePlacement,
  dmnoteRegistrationLayout,
  prepareDmnoteImport,
} from "./dmnote-registration-model";

const source = JSON.stringify({
  keys: { one: ["A"] },
  keyPositions: { one: [{ dx: 40, dy: 50, width: 60, height: 60, counter: { enabled: true } }] },
  customCSS: "old",
  tabCssOverrides: { one: "old" },
});

test("registration snapshots separate CSS, position and the global counter toggle without changing the source", () => {
  const layout = dmnoteRegistrationLayout(source);
  expect(layout).toMatchObject({ width: 120, height: 420 });
  const saved = JSON.parse(prepareDmnoteImport(source, "new", { x: 1900, y: 1000 }));
  expect(saved).toMatchObject({
    keyCounterEnabled: false,
    useCustomCSS: true,
    customCSS: { content: "new" },
    tufReplayPlacement: { x: 1800, y: 660 },
    viewport: { width: 1920, height: 1080 },
  });
  expect(saved.tabCssOverrides).toBeUndefined();
  expect(JSON.parse(source).customCSS).toBe("old");
  expect(
    JSON.parse(prepareDmnoteImport(source, undefined, { x: 0, y: 0 }, true)).keyCounterEnabled,
  ).toBe(true);
});

test("registration persists size and clamps screen coordinates against the scaled bounds", () => {
  const layout = dmnoteRegistrationLayout(source);
  expect(clampDmnotePlacement({ x: 1900, y: 1000, scale: 2 }, layout)).toEqual({
    x: 1680,
    y: 240,
    scale: 2,
  });
  expect(
    JSON.parse(prepareDmnoteImport(source, undefined, { x: 1900, y: 1000, scale: 1.5 }))
      .tufReplayPlacement,
  ).toEqual({ x: 1740, y: 450, scale: 1.5 });
});

test("legacy placements default to 100 percent and invalid values stay finite", () => {
  const layout = dmnoteRegistrationLayout(source);
  expect(clampDmnotePlacement({ x: 100, y: 200 }, layout)).toEqual({
    x: 100,
    y: 200,
    scale: 1,
  });
  expect(
    clampDmnotePlacement(
      { x: Number.NaN, y: Number.POSITIVE_INFINITY, scale: Number.NaN },
      { width: Number.NaN, height: Number.POSITIVE_INFINITY, elements: [] },
    ),
  ).toEqual({ x: 0, y: 0, scale: 1 });
  expect(clampDmnotePlacement({ x: 0, y: 0, scale: 0 }, layout).scale).toBe(0.1);
  expect(clampDmnotePlacement({ x: 0, y: 0, scale: 9 }, layout).scale).toBe(4);
});

test("registration requires a saved selection for multiple tabs", () => {
  expect(() => dmnoteRegistrationLayout('{"keys":{"one":[],"two":[]}}')).toThrow(
    "visual_selected_tab_missing",
  );
  expect(() => dmnoteRegistrationLayout('{"keys":{"one":[]},"selectedKeyType":"deleted"}')).toThrow(
    "visual_selected_tab_missing",
  );
});

test("registration imports only the selected tab and its styles, retaining shared settings", () => {
  const preset = {
    keys: { other: ["A"], numpad: ["B"] },
    selectedKeyType: "numpad",
    selectedViewerTabs: { hand: "numpad", foot: "other" },
    tabs: [{ id: "other" }, { id: "numpad", name: "Numpad" }],
    tabCount: 2,
    keyPositions: {
      other: [{ dx: 9999, activeImage: "dmnote-local-image://missing" }],
      numpad: [{ dx: 20, dy: 40, width: 80, height: 50 }],
    },
    statPositions: { other: [] },
    graphPositions: { numpad: [] },
    knobPositions: { other: [] },
    tabCssOverrides: { other: "unused", numpad: "chosen" },
    noteSettings: { trackHeight: 300 },
    tabNoteOverrides: { other: { trackHeight: 500 }, numpad: { trackHeight: 100 } },
    fontSettings: { customFonts: [] },
  };
  const json = JSON.stringify(preset);
  const layout = dmnoteRegistrationLayout(json);
  expect(layout).toMatchObject({ width: 140, height: 210, elements: [{ label: "B" }] });
  const saved = JSON.parse(prepareDmnoteImport(json, undefined, { x: 0, y: 0 }));
  expect(saved).toMatchObject({
    keys: { numpad: ["B"] },
    selectedKeyType: "numpad",
    selectedViewerTabs: { hand: "numpad" },
    tabs: [{ id: "numpad", name: "Numpad" }],
    tabCount: 1,
    statPositions: {},
    tabCssOverrides: { numpad: "chosen" },
    tabNoteOverrides: { numpad: { trackHeight: 100 } },
    fontSettings: preset.fontSettings,
  });
  expect(JSON.stringify(saved)).not.toContain("other");
  expect(JSON.stringify(preset)).toBe(json);
  const legacy = { ...preset, selectedKeyType: undefined, selected_key_type: "numpad" };
  expect(dmnoteRegistrationLayout(JSON.stringify(legacy))).toEqual(layout);
});

test("fractional scaled bounds keep integer coordinates inside the screen and input limits", () => {
  const layout = dmnoteRegistrationLayout(source);
  const saved = clampDmnotePlacement({ x: 1920, y: 1080, scale: 1.02 }, layout);
  expect(saved).toEqual({ x: 1797, y: 651, scale: 1.02 });
  expect(saved.x + layout.width * saved.scale).toBeLessThanOrEqual(1920);
  expect(saved.y + layout.height * saved.scale).toBeLessThanOrEqual(1080);
});
