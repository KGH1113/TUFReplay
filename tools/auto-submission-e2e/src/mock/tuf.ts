import { accessToken, grant, outgoingToken, owner, toolBase } from "../config";
import { fixturePath, fixtures } from "../fixtures/store";
import { catalogFileId } from "../local-tuf/catalog";
import { localTuf } from "../local-tuf/config";
import { forwardToLocalTuf } from "../local-tuf/forward";
import {
	localCatalogArchivePath,
	localCatalogArchiveUrl,
	localCatalogSnapshot,
} from "../local-tuf/snapshots";
import { log } from "../logs";
import * as receipts from "./receipts";

export async function tuf(request: Request): Promise<Response> {
	const url = new URL(request.url);
	const body = request.method === "POST" ? await request.json() : undefined;
	log(
		"tuf",
		`${request.method} ${url.pathname}${url.search}`,
		body,
		body?.run_id,
	);
	const respond = (value: unknown, status = 200) => {
		log("tuf", `${status} ${url.pathname}`, value, body?.run_id);
		return Response.json(value, { status });
	};
	const items = await fixtures();
	if (
		url.pathname.startsWith("/v2/internal/") &&
		request.headers.get("authorization") !== `Bearer ${outgoingToken}`
	)
		return respond({ error: "unauthorized" }, 401);
	if (localTuf && url.pathname.startsWith("/v2/internal/auto-submission/"))
		return forwardToLocalTuf(request, body);
	if (
		url.pathname.endsWith("/identity") ||
		url.pathname.endsWith("/authorization")
	) {
		if (
			body?.access_token !== undefined
				? body.access_token !== accessToken
				: body?.owner_id !== owner || body?.grant_id !== grant
		)
			return respond({ error: "unauthorized" }, 401);
		return respond({
			owner_id: owner,
			grant_id: grant,
			client_id: "e2e-local-client",
			username: "local-tester",
			nickname: "Local Tester",
		});
	}
	const level = url.pathname.match(/^\/v2\/database\/levels\/byId\/(\d+)$/);
	if (level) {
		const levelId = Number(level[1]);
		const item = items.find((f) => f.levelId === levelId);
		const fileId = item && catalogFileId(item, localTuf);
		if (item && !fileId)
			return respond({ error: "local_tuf_fixture_not_synced" }, 503);
		if (item)
			return respond({
					id: item.levelId,
					// The local chart archive remains the recorded fixture. Registration still needs
					// the current local TUF revision ID as immutable audit metadata.
					fileId,
					dlLink: `${toolBase}/archives/${item.id}.zip`,
					difficulty: { type: "PGU", name: "P1" },
					isHidden: false,
					isDeleted: false,
				});
		const snapshot = localTuf && (await localCatalogSnapshot(levelId));
		return snapshot
			? respond({
					id: snapshot.levelId,
					fileId: snapshot.fileId,
					dlLink: localCatalogArchiveUrl(snapshot.levelId),
					difficulty: snapshot.difficulty,
					isHidden: false,
					isDeleted: false,
				})
			: respond({ error: "not_found" }, 404);
	}
	const catalogArchive = url.pathname.match(
		/^\/catalog-archives\/(\d+)\.zip$/,
	);
	if (catalogArchive) {
		const levelId = Number(catalogArchive[1]);
		const snapshot = localTuf && (await localCatalogSnapshot(levelId));
		if (!snapshot) return respond({ error: "not_found" }, 404);
		log("tuf", "200 local installed chart ZIP", { levelId });
		return new Response(Bun.file(localCatalogArchivePath(levelId)), {
			headers: { "Content-Type": "application/zip" },
		});
	}
	const archive = url.pathname.match(/^\/archives\/([a-zA-Z0-9-]+)\.zip$/);
	if (archive) {
		const item = items.find((f) => f.id === archive[1]);
		if (!item) return respond({ error: "not_found" }, 404);
		log("tuf", "200 local chart ZIP", { fixtureId: item.id });
		return new Response(Bun.file(fixturePath(item, "chart.zip")), {
			headers: { "Content-Type": "application/zip" },
		});
	}
	const receipt = url.pathname.match(
		/^\/v2\/internal\/auto-submission\/receipts\/([a-f0-9-]+)$/,
	);
	if (receipt) {
		const found = receipts.lookup(receipt[1]);
		if (!found) return respond({ error: "not_found" }, 404);
		if (
			found.owner_id !== url.searchParams.get("owner_id") ||
			found.digest !== url.searchParams.get("evidence_digest")
		)
			return respond({ error: "identity_mismatch" }, 409);
		return respond({ pass_id: found.pass_id });
	}
	if (url.pathname === "/v2/internal/auto-submission/register") {
		if (
			body?.owner_id !== owner ||
			body?.grant_id !== grant ||
			!body?.validation?.validator_version?.startsWith("e2e-fixture-")
		)
			return respond({ error: "invalid_fixture" }, 422);
		const item = items.find((f) => f.levelId === body.level_id);
		if (!item || body.current_file_id !== item.fileId)
			return respond({ error: "level_revision_changed" }, 409);
		const result = receipts.register(body);
		if (result.lose) {
			log(
				"tuf",
				"등록 저장 후 응답 유실 주입",
				{ run_id: body.run_id, pass_id: result.pass_id },
				body.run_id,
			);
			return new Response(
				new ReadableStream({
					start(controller) {
						controller.error(new Error("injected lost response"));
					},
				}),
			);
		}
		return respond({ pass_id: result.pass_id });
	}
	return respond({ error: "unimplemented_mock_route" }, 404);
}
