// OrbitRender 2.8.0 is the first that renders with TUFReplay replaying the recording.
// Older versions replay it themselves and stop on keys the game's input cannot take.
// Keep in sync with OrbitRenderClient.MinimumVersion in the mod.
const minimum = [2, 8, 0] as const;

/** The oldest OrbitRender that can make videos, for messages. */
export const minimumOrbitRenderVersion = minimum.join(".");

export function orbitVersionCompatibility(version: string | null | undefined) {
  const match =
    typeof version === "string" ? /^(\d+)\.(\d+)\.(\d+)(?:\.\d+)?$/.exec(version) : null;
  if (!match) return "unknown";
  for (let index = 0; index < minimum.length; index++) {
    const part = Number(match[index + 1]);
    if (part !== minimum[index]) return part > minimum[index] ? "supported" : "unsupported";
  }
  return "supported";
}
