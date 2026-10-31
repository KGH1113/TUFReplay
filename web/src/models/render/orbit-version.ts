export function orbitVersionCompatibility(version: string | null | undefined) {
  const match =
    typeof version === "string" ? /^(\d+)\.(\d+)\.(\d+)(?:\.\d+)?$/.exec(version) : null;
  if (!match) return "unknown";
  const major = Number(match[1]);
  const minor = Number(match[2]);
  return major < 2 || (major === 2 && minor < 7) ? "unsupported" : "supported";
}
