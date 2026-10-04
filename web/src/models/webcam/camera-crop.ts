import type { CameraCrop } from "@/models/webcam/webcam-model";

export const FULL_CAMERA_CROP: CameraCrop = { x: 0, y: 0, width: 1, height: 1 };
export const MIN_CAMERA_CROP = 0.05;
export type CameraCropHandle = "move" | "nw" | "ne" | "sw" | "se";

export function displayCameraCrop(crop: CameraCrop, mirrored: boolean): CameraCrop {
  return mirrored ? { ...crop, x: 1 - crop.x - crop.width } : crop;
}

export function changeCameraCrop(
  crop: CameraCrop,
  handle: CameraCropHandle,
  dx: number,
  dy: number,
): CameraCrop {
  if (handle === "move") {
    return {
      ...crop,
      x: clamp(crop.x + dx, 0, 1 - crop.width),
      y: clamp(crop.y + dy, 0, 1 - crop.height),
    };
  }
  const right = crop.x + crop.width;
  const bottom = crop.y + crop.height;
  const left = handle.endsWith("w") ? clamp(crop.x + dx, 0, right - MIN_CAMERA_CROP) : crop.x;
  const top = handle.startsWith("n") ? clamp(crop.y + dy, 0, bottom - MIN_CAMERA_CROP) : crop.y;
  const nextRight = handle.endsWith("e") ? clamp(right + dx, left + MIN_CAMERA_CROP, 1) : right;
  const nextBottom = handle.startsWith("s") ? clamp(bottom + dy, top + MIN_CAMERA_CROP, 1) : bottom;
  return { x: left, y: top, width: nextRight - left, height: nextBottom - top };
}

export function cameraCropEqual(left: CameraCrop, right: CameraCrop) {
  return (["x", "y", "width", "height"] as const).every(
    (key) => Math.abs(left[key] - right[key]) < 0.000001,
  );
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value));
}
