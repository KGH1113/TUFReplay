/**
 * Centers, in pixels, for markers placed along a track by their position in the level.
 * Markers keep their order and at least `minDistance` apart, and stay inside the track,
 * so nearby start tiles stay clickable instead of stacking on top of each other.
 */
export function layoutTimelineMarkers(
  fractions: readonly number[],
  width: number,
  markerSize: number,
  minDistance: number,
) {
  const radius = markerSize / 2;
  const low = radius;
  const high = Math.max(low, width - radius);
  const centers = fractions.map((fraction) =>
    Math.min(high, Math.max(low, low + Math.min(1, Math.max(0, fraction)) * (high - low))),
  );
  for (let index = 1; index < centers.length; index++)
    centers[index] = Math.max(centers[index], centers[index - 1] + minDistance);
  // Markers pushed past the end slide back, still keeping their spacing where room allows.
  if (centers.length > 0) centers[centers.length - 1] = Math.min(centers[centers.length - 1], high);
  for (let index = centers.length - 2; index >= 0; index--)
    centers[index] = Math.min(centers[index], centers[index + 1] - minDistance);
  return centers.map((center) => Math.max(low, center));
}
