import { confirmedArchivePath } from "./official-chart";

export interface OfficialArchive {
	levelId: number;
	fileId: string;
	archive: string;
	metadata: unknown;
}

/** Serve the exact metadata paired with the downloaded official ZIP. */
export function createArchiveHandler(charts: OfficialArchive[]) {
	for (const chart of charts) {
		if (!chart.fileId) throw new Error("Official chart file ID is missing");
		confirmedArchivePath(chart.metadata);
	}
	return (request: Request): Response => {
		const url = new URL(request.url);
		if (url.host !== "127.0.0.1:5152")
			return new Response("Invalid host", { status: 403 });
		if (request.method !== "GET" && request.method !== "HEAD")
			return new Response(null, { status: 405 });
		if (url.pathname === "/health")
			return Response.json({
				mode: "local-game",
				charts: charts.map((x) => x.levelId),
			});
		const metadataMatch = /^\/cdn\/([^/]+)\/metadata$/.exec(url.pathname);
		if (metadataMatch) {
			const chart = charts.find((c) => c.fileId === metadataMatch[1]);
			return chart
				? Response.json({ metadata: chart.metadata })
				: Response.json({ error: "File not found" }, { status: 404 });
		}
		const match = /^\/charts\/([1-9]\d*)\.zip$/.exec(url.pathname);
		const chart = match && charts.find((c) => c.levelId === Number(match[1]));
		return chart
			? new Response(Bun.file(chart.archive), {
					headers: {
						"Content-Type": "application/zip",
						"Access-Control-Allow-Origin": "http://127.0.0.1:5190",
					},
				})
			: new Response("Not found", { status: 404 });
	};
}
