import { describe, expect, it } from "bun:test";
import {
  visualNameError,
  visualPresetAvatar,
  visualResponseBelongsToAccount,
  visualSourceSupportsKind,
} from "@/models/visual/visual-model";

describe("visual preset model", () => {
  it("uses the first grapheme for gallery artwork", () => {
    expect(visualPresetAvatar("👩‍💻 keys")).toBe("👩‍💻");
    expect(visualPresetAvatar("Aurora")).toBe("A");
  });

  it("enforces the user-facing name range without deduplicating registrations", () => {
    expect(visualNameError("   ")).toBe("visual_name_required");
    expect(visualNameError("a".repeat(81))).toBe("visual_name_too_long");
    expect(visualNameError("Aurora keys")).toBeNull();
  });

  it("limits DMNote to keyviewer registrations", () => {
    const dmnote = {
      source: "dmnote" as const,
      version: "1",
      available: true,
      kinds: ["keyviewer" as const],
    };
    expect(visualSourceSupportsKind(dmnote, "keyviewer")).toBe(true);
    expect(visualSourceSupportsKind(dmnote, "overlay")).toBe(false);
  });

  it("requires automatic installation detection for installed sources", () => {
    for (const source of ["jipper-resourcepack"] as const) {
      const info = {
        source,
        version: "1",
        available: false,
        kinds: ["keyviewer", "overlay"] as const,
      };
      expect(visualSourceSupportsKind({ ...info, kinds: [...info.kinds] }, "keyviewer")).toBe(
        false,
      );
      expect(
        visualSourceSupportsKind({ ...info, available: true, kinds: [...info.kinds] }, "overlay"),
      ).toBe(true);
    }
  });

  it("rejects a delayed response after the connected account changes", async () => {
    const response = Promise.resolve({ presets: [] });
    const requestedAccount = "username:account-a";
    let currentAccount = requestedAccount;

    expect(visualResponseBelongsToAccount(requestedAccount, currentAccount)).toBe(true);
    currentAccount = "username:account-b";

    await response;
    expect(visualResponseBelongsToAccount(requestedAccount, currentAccount)).toBe(false);
  });
});
