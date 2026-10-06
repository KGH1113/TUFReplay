import { describe, expect, test } from "bun:test";
import { changeCameraCrop, displayCameraCrop, FULL_CAMERA_CROP } from "@/models/webcam/camera-crop";

describe("camera crop geometry", () => {
  test("moves a crop without changing its size and keeps it in the source image", () => {
    const crop = { x: 0.1, y: 0.2, width: 0.6, height: 0.5 };
    expect(changeCameraCrop(crop, "move", 1, -1)).toEqual({
      x: 0.4,
      y: 0,
      width: 0.6,
      height: 0.5,
    });
    expect(changeCameraCrop(FULL_CAMERA_CROP, "move", 0.4, 0.4)).toEqual(FULL_CAMERA_CROP);
  });

  test.each([
    "nw",
    "ne",
    "sw",
    "se",
  ] as const)("anchors the opposite corner while resizing %s", (handle) => {
    const crop = { x: 0.2, y: 0.2, width: 0.6, height: 0.6 };
    const result = changeCameraCrop(crop, handle, 0.1, -0.1);
    expect(result.width).toBeCloseTo(handle.endsWith("w") ? 0.5 : 0.7);
    expect(result.height).toBeCloseTo(handle.startsWith("n") ? 0.7 : 0.5);
    expect(handle.endsWith("w") ? result.x + result.width : result.x).toBeCloseTo(
      handle.endsWith("w") ? 0.8 : 0.2,
    );
    expect(handle.startsWith("n") ? result.y + result.height : result.y).toBeCloseTo(
      handle.startsWith("n") ? 0.8 : 0.2,
    );
  });

  test("cannot invert a crop or resize outside the image", () => {
    const result = changeCameraCrop(FULL_CAMERA_CROP, "nw", 4, 4);
    expect(result.width).toBeCloseTo(0.05);
    expect(result.height).toBeCloseTo(0.05);
    expect(changeCameraCrop(FULL_CAMERA_CROP, "se", 4, 4)).toEqual(FULL_CAMERA_CROP);
  });

  test("mirrored editing saves stable source coordinates", () => {
    const source = { x: 0.1, y: 0.2, width: 0.6, height: 0.5 };
    const display = displayCameraCrop(source, true);
    expect(display.x).toBeCloseTo(0.3);
    const moved = displayCameraCrop(changeCameraCrop(display, "move", 0.1, 0), true);
    expect(moved.x).toBeCloseTo(0);
    expect(moved.width).toBe(source.width);
    const resized = displayCameraCrop(changeCameraCrop(display, "ne", -0.1, 0), true);
    expect(resized.x).toBeCloseTo(0.2);
    expect(resized.width).toBeCloseTo(0.5);
    expect(displayCameraCrop(display, true).x).toBeCloseTo(source.x);
  });
});
