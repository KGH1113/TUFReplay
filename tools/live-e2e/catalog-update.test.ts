import { describe, expect, it } from "bun:test";
import { localCatalogPatch } from "./catalog-update";

const chart = { levelId: 8068, fileId: "installed-file" };
const localUrl = "http://127.0.0.1:5152/charts/8068.zip";
describe("local chart identity", () => {
	it("preserves file identity when replacing a CDN URL and on repeated preparation", () => {
		const patch = localCatalogPatch(
			{
				id: 8068,
				fileId: chart.fileId,
				dlLink: "https://cdn.example/chart.zip",
			},
			chart,
		);
		expect(patch).toEqual({ fileId: chart.fileId, dlLink: localUrl });
		expect(localCatalogPatch({ id: 8068, ...patch }, chart)).toEqual(patch);
	});
	it("repairs the null ID left by the old local seed's CDN hook", () => {
		expect(
			localCatalogPatch({ id: 8068, fileId: null, dlLink: localUrl }, chart),
		).toEqual({ fileId: chart.fileId, dlLink: localUrl });
	});
	it("never overwrites a different revision, even at the runner's URL", () => {
		expect(() =>
			localCatalogPatch(
				{ id: 8068, fileId: "another-revision", dlLink: localUrl },
				chart,
			),
		).toThrow("differs");
	});
	it("does not repair null IDs belonging to other URLs or levels", () => {
		for (const dlLink of [
			null,
			"https://cdn.example/chart.zip",
			"http://127.0.0.1:5152/charts/3072.zip",
		])
			expect(() =>
				localCatalogPatch({ id: 8068, fileId: null, dlLink }, chart),
			).toThrow("differs");
		expect(() => localCatalogPatch(null, chart)).toThrow("missing");
		expect(() =>
			localCatalogPatch(
				{ id: 3072, fileId: chart.fileId, dlLink: localUrl },
				chart,
			),
		).toThrow("missing");
	});
});
