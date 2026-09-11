import { describe, expect, test } from "bun:test";

import { formatFileSize } from "@/models/activity/file-size";

describe("file size formatting", () => {
  test("formats microphone recording sizes for compact run metadata", () => {
    expect(formatFileSize(0, "en-US")).toBe("0 B");
    expect(formatFileSize(512, "en-US")).toBe("512 B");
    expect(formatFileSize(1_536, "en-US")).toBe("1.5 KB");
    expect(formatFileSize(18.4 * 1024 ** 2, "en-US")).toBe("18.4 MB");
    expect(formatFileSize(1.25 * 1024 ** 3, "en-US")).toBe("1.3 GB");
  });
});
