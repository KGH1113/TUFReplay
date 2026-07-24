import { describe, expect, test } from "bun:test";

import { formatFileSize } from "./file-size.format";

describe("file size formatting", () => {
  test("formats microphone recording sizes for compact run metadata", () => {
    expect(formatFileSize(0)).toBe("0 B");
    expect(formatFileSize(512)).toBe("512 B");
    expect(formatFileSize(1_536)).toBe("1.5 KB");
    expect(formatFileSize(18.4 * 1024 ** 2)).toBe("18.4 MB");
    expect(formatFileSize(1.25 * 1024 ** 3)).toBe("1.3 GB");
  });
});
