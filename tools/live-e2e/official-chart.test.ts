import { describe, expect, test } from "bun:test";
import { confirmedArchivePath, verifyArchiveSelection } from "./official-chart";

const metadata = {
	pathConfirmed: true,
	targetLevel: "levels/id/levelEX.adofai",
	levelFiles: {
		"Merry Christmas/level.adofai": { path: "levels/id/level.adofai" },
		"Merry Christmas/levelEX.adofai": { path: "levels/id/levelEX.adofai" },
	},
};

describe("official local chart fixture", () => {
	test("retains the original confirmed ZIP path", () => {
		expect(confirmedArchivePath(metadata)).toBe(
			"Merry Christmas/levelEX.adofai",
		);
		expect(() =>
			verifyArchiveSelection(metadata, Object.keys(metadata.levelFiles)),
		).not.toThrow();
	});
	test("rejects missing metadata and flattened installed archives", () => {
		expect(() => confirmedArchivePath(undefined)).toThrow();
		expect(() =>
			verifyArchiveSelection(metadata, ["level.adofai", "levelEX.adofai"]),
		).toThrow();
	});
	test("does not invent confirmation or resolve duplicate selections", () => {
		expect(
			confirmedArchivePath({ ...metadata, pathConfirmed: false }),
		).toBeNull();
		expect(() =>
			confirmedArchivePath({
				...metadata,
				levelFiles: {
					...metadata.levelFiles,
					duplicate: { path: metadata.targetLevel },
				},
			}),
		).toThrow();
	});
});
