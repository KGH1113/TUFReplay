#!/usr/bin/env node

import { readFile, rename, stat, unlink, writeFile } from "node:fs/promises";
import { basename, dirname, extname, join, resolve } from "node:path";
import process from "node:process";

// Keep this list aligned with GameplayChartHash.WriteGameplayEvent. These are
// the level actions that affect the timing or judgment path of a play.
const GAMEPLAY_EVENT_TYPES = new Set([
  "SetSpeed",
  "Twirl",
  "Hold",
  "MultiPlanet",
  "Pause",
  "AutoPlayTiles",
  "ScaleMargin",
  "Multitap",
  "KillPlayer",
]);

// LevelEvent.Decode supplies game defaults for omitted global settings. Keep
// only settings consumed by song timing/audio, countdown behavior, optional
// speed-trial gameplay, or the tile-data decoder. Track, background, camera,
// metadata, editor, and visual compatibility settings intentionally fall back
// to their defaults.
const GAMEPLAY_SETTING_KEYS = new Set([
  "version",
  "separateCountdownTime",
  "speedTrialAim",
  "songFilename",
  "bpm",
  "volume",
  "offset",
  "pitch",
  "hitsound",
  "hitsoundVolume",
  "countdownTicks",
  "legacySpriteTiles",
]);

function usage() {
  const script = basename(process.argv[1]);
  console.error(`Usage: node scripts/${script} <input.adofai> [output.adofai]`);
}

function defaultOutputPath(inputPath) {
  const extension = extname(inputPath);
  const stem = basename(inputPath, extension);
  return join(dirname(inputPath), `${stem}.gameplay${extension || ".adofai"}`);
}

// Older ADOFAI files can contain trailing commas. Remove only commas that are
// outside JSON strings and immediately precede a closing array or object.
function removeTrailingCommas(source) {
  let result = "";
  let inString = false;
  let escaped = false;

  for (let index = 0; index < source.length; index += 1) {
    const character = source[index];

    if (inString) {
      result += character;
      if (escaped) escaped = false;
      else if (character === "\\") escaped = true;
      else if (character === '"') inString = false;
      continue;
    }

    if (character === '"') {
      inString = true;
      result += character;
      continue;
    }

    if (character === ",") {
      let lookahead = index + 1;
      while (/\s/.test(source[lookahead] ?? "")) lookahead += 1;
      if (source[lookahead] === "]" || source[lookahead] === "}") continue;
    }

    result += character;
  }

  return result;
}

function parseLevel(source) {
  const withoutBom = source.replace(/^\uFEFF/, "");
  try {
    return JSON.parse(withoutBom);
  } catch (firstError) {
    try {
      return JSON.parse(removeTrailingCommas(withoutBom));
    } catch {
      throw firstError;
    }
  }
}

function assertLevelShape(level) {
  if (level === null || Array.isArray(level) || typeof level !== "object") {
    throw new Error("The input root must be a JSON object.");
  }
  if (typeof level.pathData !== "string" && !Array.isArray(level.angleData)) {
    throw new Error("The input has neither pathData nor angleData tile data.");
  }
  if (
    level.settings === null ||
    Array.isArray(level.settings) ||
    typeof level.settings !== "object"
  ) {
    throw new Error("The input settings field must be a JSON object.");
  }
  if (level.actions !== undefined && !Array.isArray(level.actions)) {
    throw new Error("The input actions field must be an array.");
  }
  if (level.decorations !== undefined && !Array.isArray(level.decorations)) {
    throw new Error("The input decorations field must be an array.");
  }
}

async function main() {
  const [, , inputArgument, outputArgument] = process.argv;
  if (!inputArgument || process.argv.length > 4) {
    usage();
    process.exitCode = 2;
    return;
  }

  const inputPath = resolve(inputArgument);
  const outputPath = resolve(outputArgument ?? defaultOutputPath(inputPath));
  if (inputPath === outputPath) {
    throw new Error("Refusing to overwrite the input file. Choose a different output path.");
  }

  const source = await readFile(inputPath, "utf8");
  const level = parseLevel(source);
  assertLevelShape(level);

  const originalActions = level.actions ?? [];
  const originalDecorations = level.decorations ?? [];
  const originalSettings = level.settings;
  const keptActions = originalActions.filter((action) =>
    GAMEPLAY_EVENT_TYPES.has(action?.eventType),
  );
  const keptSettings = Object.fromEntries(
    Object.entries(originalSettings).filter(([key]) => GAMEPLAY_SETTING_KEYS.has(key)),
  );

  level.settings = keptSettings;
  level.actions = keptActions;
  level.decorations = [];

  const temporaryPath = `${outputPath}.tmp-${process.pid}`;
  try {
    await writeFile(temporaryPath, `${JSON.stringify(level, null, 2)}\n`, {
      encoding: "utf8",
      flag: "wx",
    });
    await rename(temporaryPath, outputPath);
  } catch (error) {
    await unlink(temporaryPath).catch(() => {});
    throw error;
  }

  const outputSize = (await stat(outputPath)).size;
  const keptCounts = Object.groupBy
    ? Object.groupBy(keptActions, (action) => action.eventType)
    : keptActions.reduce((counts, action) => {
        counts[action.eventType] ??= [];
        counts[action.eventType].push(action);
        return counts;
      }, {});
  const eventSummary = Object.entries(keptCounts)
    .map(([eventType, actions]) => `${eventType}=${actions.length}`)
    .join(", ");

  console.log(`Wrote ${outputPath}`);
  console.log(
    `Kept ${keptActions.length}/${originalActions.length} actions (${eventSummary || "none"}); ` +
      `kept ${Object.keys(keptSettings).length}/${Object.keys(originalSettings).length} settings; ` +
      `removed ${originalDecorations.length} decorations; output ${outputSize} bytes.`,
  );
}

main().catch((error) => {
  console.error(`Error: ${error.message}`);
  process.exitCode = 1;
});
