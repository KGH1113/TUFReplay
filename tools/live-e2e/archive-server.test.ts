import { expect, test } from "bun:test";
import { createArchiveHandler } from "./archive-server";
import { confirmedArchivePath } from "./official-chart";

const metadata = {
	pathConfirmed: true,
	targetLevel: "levels/file/levelEX.adofai",
	levelFiles: {
		"folder/level.adofai": { path: "levels/file/level.adofai" },
		"folder/levelEX.adofai": { path: "levels/file/levelEX.adofai" },
	},
};
const handler = createArchiveHandler([
	{ levelId: 8068, fileId: "file", archive: "unused.zip", metadata },
]);

test("local CDN endpoint preserves the confirmed original EX chart selection", async () => {
	const response = handler(
		new Request("http://127.0.0.1:5152/cdn/file/metadata"),
	);
	expect(response.status).toBe(200);
	const body = await response.json();
	expect(body).toEqual({ metadata });
	expect(confirmedArchivePath(body.metadata)).toBe("folder/levelEX.adofai");
});

test("metadata service fails closed for unknown file IDs, writes and remote hosts", () => {
	expect(
		handler(new Request("http://127.0.0.1:5152/cdn/unknown/metadata")).status,
	).toBe(404);
	expect(
		handler(
			new Request("http://127.0.0.1:5152/cdn/file/metadata", {
				method: "POST",
			}),
		).status,
	).toBe(405);
	expect(
		handler(new Request("http://example.com:5152/cdn/file/metadata")).status,
	).toBe(403);
	expect(() =>
		createArchiveHandler([
			{ levelId: 8068, fileId: "file", archive: "unused", metadata: null },
		]),
	).toThrow();
});

test("local server queries CDN metadata separately from the real TUF API", async () => {
	const config = await Bun.file(
		new URL("../../server/config/local-game.yaml", import.meta.url),
	).text();
	expect(config).toContain("tuf_api_base_url: http://127.0.0.1:3002");
	expect(config).toContain("tuf_metadata_base_url: http://127.0.0.1:5152");
});
