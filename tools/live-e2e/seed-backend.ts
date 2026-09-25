import { randomBytes } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { localCatalogPatch } from "./catalog-update.ts";
import { confirmedArchivePath } from "./official-chart.ts";
import {
	assertBackendEnvironment,
	backend,
	callback,
	clientId,
	data,
	owner,
	username,
} from "./config.ts";

// Run with Node/tsx from the backend cwd, so its own aliases and native dependencies apply.
assertBackendEnvironment(process.env);
const load = (path: string) => import(pathToFileURL(`${backend}/${path}`).href);
const { default: db } = await load("src/models/index.ts");
const { default: CdnFile } = await load("src/models/cdn/CdnFile.ts");
const { getPoolManagerInstance } = await load("src/config/db.ts");
const { passwordUtils } = await load("src/misc/utils/auth/auth.ts");
await mkdir(data, { recursive: true, mode: 0o700 });
try {
	let credentials: { username: string; password: string };
	try {
		credentials = JSON.parse(await readFile(`${data}/login.json`, "utf8"));
	} catch (error) {
		if (!(error instanceof Error && "code" in error && error.code === "ENOENT"))
			throw error;
		credentials = { username, password: randomBytes(24).toString("base64url") };
		await writeFile(
			`${data}/login.json`,
			`${JSON.stringify(credentials, null, 2)}\n`,
			{ mode: 0o600 },
		);
	}
	if (credentials.username !== username || credentials.password.length < 24)
		throw new Error("Invalid private local login file");
	const [player] = await db.models.Player.findOrCreate({
		where: { name: "Local Game E2E Tester" },
		defaults: { name: "Local Game E2E Tester", country: "KR" },
	});
	const password = await passwordUtils.hashPassword(credentials.password);
	const [user, created] = await db.models.User.findOrCreate({
		where: { id: owner },
		defaults: {
			id: owner,
			username,
			nickname: "Local Game E2E Tester",
			playerId: player.id,
			password,
			status: "active",
			permissionFlags: 1,
		},
	});
	if (!created && (user.username !== username || user.playerId !== player.id))
		throw new Error("Local test account ID belongs to another account");
	// Only this dedicated account is maintained; existing user credentials are untouched.
	if (!created) await user.update({ password });
	const [client, newClient] = await db.models.OAuthClient.findOrCreate({
		where: { clientId },
		defaults: {
			clientId,
			ownerUserId: owner,
			name: "TUFReplay Local Game E2E",
			redirectUris: [callback],
			allowedScopes: "65537",
			status: "active",
		},
	});
	if (!newClient) {
		if (client.ownerUserId !== owner)
			throw new Error("Local OAuth client ownership mismatch");
		await client.update({
			redirectUris: [callback],
			allowedScopes: "65537",
			status: "active",
		});
	}
	// Do not mint a token or create a grant: browser login and PKCE consent must do that.
	const manifest = JSON.parse(await readFile(`${data}/charts.json`, "utf8"));
	const transaction = await db.models.Level.sequelize.transaction();
	try {
		for (const chart of manifest) {
			// Reject stale manifests produced by the installed-folder ZIP workflow.
			confirmedArchivePath(chart.metadata);
			const level = await db.models.Level.findByPk(chart.levelId, {
				attributes: ["id", "song", "diffId", "fileId", "dlLink"],
				transaction,
				lock: transaction.LOCK.UPDATE,
			});
			const patch = localCatalogPatch(level, chart);
			const difficulty = await db.models.Difficulty.findByPk(level.diffId, {
				transaction,
			});
			if (
				difficulty?.type !== "PGU" ||
				!/^[PG](?:[1-9]|1[0-9]|20)$/.test(difficulty?.name ?? "")
			)
				throw new Error(
					`TUF #${chart.levelId}: only existing P/G levels are eligible`,
				);
			// Guarded by assertBackendEnvironment above. This fixture URL is not a
			// CDN URL: preserve its installed identity explicitly, in the same write.
			await CdnFile.upsert(
				{
					id: chart.fileId,
					type: "LEVELZIP",
					filePath: chart.archive,
					metadata: chart.metadata,
				},
				{ transaction },
			);
			await level.update(patch, {
				transaction,
				hooks: false,
				fields: ["dlLink", "fileId"],
			});
			chart.song = level.song;
			chart.difficulty = difficulty.name;
		}
		await transaction.commit();
	} catch (error) {
		await transaction.rollback();
		throw error;
	}
	await writeFile(
		`${data}/charts.json`,
		`${JSON.stringify(manifest, null, 2)}\n`,
	);
	console.log(
		`Local account and PKCE client prepared. Credentials: ${data}/login.json`,
	);
} finally {
	await getPoolManagerInstance().closeAllPools();
}
